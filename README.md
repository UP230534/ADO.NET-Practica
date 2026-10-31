# ProductosAPI

Backend con operaciones CRUD sobre una entidad `Producto`, hecho con ASP.NET Core
(minimal API) sobre .NET 10, acceso a datos con **ADO.NET** y base de datos
**SQL Server**. Se prueba desde Swagger.

Resuelve los seis puntos de la actividad: conexión, consultas, insert, actualizar,
eliminar y buscar.

## Cómo correrlo

1. Crear la base de datos (una sola vez):

   ```
   sqlcmd -S "(localdb)\MSSQLLocalDB" -i "ProductosAPI\Database\esquema.sql"
   ```

   El script crea `TiendaDb`, la tabla `Producto` y cuatro registros de prueba.

2. Levantar la API:

   ```
   cd ProductosAPI
   dotnet run
   ```

3. Se abre `http://localhost:5050/swagger`, que es donde se prueban los endpoints.

## Estructura del proyecto

Cada carpeta tiene una sola responsabilidad, para que el código de SQL no se
mezcle con el de las rutas HTTP.

| Carpeta | Qué hace |
|---|---|
| `Data/` | `ConexionSql`, la única clase que conoce la cadena de conexión. |
| `Models/` | `Producto`, la clase que viaja como JSON y que representa un renglón de la tabla. |
| `Repositories/` | `IProductoRepository` dice **qué** se puede hacer; `ProductoRepository` dice **cómo** se hace con ADO.NET. Aquí vive todo el SQL. |
| `Endpoints/` | Las rutas HTTP. Validan la entrada y llaman al repositorio; no saben nada de SQL. |
| `Database/` | `esquema.sql`, el script que crea la base y la tabla. |

## La tabla y el modelo

```sql
CREATE TABLE Producto (
    Id       INT IDENTITY(1,1) PRIMARY KEY,
    Nombre   NVARCHAR(100) NOT NULL,
    Precio   DECIMAL(10,2) NOT NULL,
    Cantidad INT           NOT NULL
);
```

La clase `Producto` tiene esas mismas cuatro propiedades. `Id` es `IDENTITY`, así
que lo genera SQL Server al insertar. `Precio` es `DECIMAL(10,2)` y no `FLOAT`
porque con dinero no conviene usar decimales aproximados.

## El flujo de una petición

```
Cliente (Swagger)  ->  Endpoint  ->  IProductoRepository  ->  ADO.NET  ->  SQL Server
```

Por ejemplo, al mandar `DELETE /productos/3`:

1. El endpoint recibe el id y comprueba que sea un entero (`{id:int}` en la ruta).
2. Llama a `repo.Eliminar(3)`. El endpoint solo conoce la **interfaz**, nunca la
   clase concreta.
3. `ProductoRepository` abre una conexión, arma el `DELETE` con el id como
   parámetro y lo ejecuta.
4. Devuelve `true` si borró un renglón y `false` si no existía.
5. El endpoint traduce eso a `204 No Content` o a `404 Not Found`.

Quien decide **qué** responder es el endpoint; quien sabe **hablar con la base**
es el repositorio. Esa separación es la idea central del proyecto.

## La conexión, sin exponerla

La cadena de conexión no está escrita en el código: vive en `appsettings.json` y
una sola clase la lee, pidiéndola por nombre.

```csharp
_cadena = configuration.GetConnectionString("Tienda");
public SqlConnection Crear() => new SqlConnection(_cadena);
```

`ConexionSql` funciona como una fábrica: quien necesite hablar con la base pide
una conexión nueva. Si mañana cambiara el servidor, se toca un solo archivo de
configuración y nada del código.

En `Program.cs` se registran las dependencias una sola vez:

```csharp
builder.Services.AddSingleton<ConexionSql>();
builder.Services.AddScoped<IProductoRepository, ProductoRepository>();
```

Eso es inyección de dependencias: cuando un endpoint pide un `IProductoRepository`,
.NET le entrega un `ProductoRepository` ya armado con su conexión adentro.
`Singleton` significa uno para toda la aplicación y `Scoped` uno nuevo por
petición. Gracias a esto, cambiar a MySQL sería escribir otra clase que implemente
la misma interfaz y modificar ese renglón.

## Los métodos del repositorio

Todos siguen el mismo patrón: abrir la conexión con `using` (que la cierra sola
aunque truene), armar un `SqlCommand` y pasar los valores como **parámetros**
(`@Nombre`, `@Id`) en lugar de pegarlos al texto del SQL. Eso es lo que evita la
inyección de SQL.

| Método | Qué hace | Cómo ejecuta |
|---|---|---|
| `ObtenerTodos()` | Trae todos los productos. | `ExecuteReader`, recorriendo las filas con `Read()`. |
| `ObtenerPorId(int id)` | Trae uno solo, o `null` si no existe. | `ExecuteReader` con una sola fila. |
| `Buscar(string texto)` | Los productos cuyo nombre contenga el texto. | `SqlDataAdapter` + `DataTable`. |
| `Insertar(Producto p)` | Da de alta y devuelve el `Id` generado. | `ExecuteScalar`, leyendo `SCOPE_IDENTITY()`. |
| `Actualizar(Producto p)` | Modifica y devuelve si encontró el renglón. | `ExecuteNonQuery() > 0`. |
| `Eliminar(int id)` | Borra y devuelve si encontró el renglón. | `ExecuteNonQuery() > 0`. |

Dos detalles que vale la pena explicar:

**`SCOPE_IDENTITY()` en el insert.** La tabla tiene `Id` como `IDENTITY`, así que
el número lo genera SQL Server, no la aplicación. Para poder devolverlo, el
`INSERT` y la lectura del id van en el **mismo comando**:

```sql
INSERT INTO Producto (Nombre, Precio, Cantidad) VALUES (@Nombre, @Precio, @Cantidad);
SELECT CAST(SCOPE_IDENTITY() AS INT);
```

**`ExecuteNonQuery() > 0` en actualizar y eliminar.** Ese método devuelve cuántos
renglones cambiaron. Si devuelve cero significa que ese id no existía, y por eso
los métodos regresan `bool`: así el endpoint puede responder `404` en vez de
fingir que el cambio funcionó.

## Por qué `Buscar` usa DataAdapter

Los demás métodos usan `SqlDataReader`, que es **modo conectado**: va leyendo
fila por fila mientras la conexión sigue abierta. Es lo más rápido y lo que
conviene casi siempre.

`Buscar` usa `SqlDataAdapter` con un `DataTable`, que es **modo desconectado**:
trae todo el resultado de un jalón a memoria y luego se recorre la tabla ya sin
la base de por medio. Se nota en un detalle: es el único método donde no escribo
`cn.Open()`, porque el adaptador abre y cierra la conexión él solo dentro de
`Fill()`.

Lo implementé así a propósito, para tener las dos formas de acceso a datos de
ADO.NET dentro del mismo proyecto y poder compararlas.

La búsqueda es por coincidencia parcial, con `LIKE` y el texto entre comodines:

```csharp
cmd.Parameters.AddWithValue("@Texto", $"%{texto}%");
```

Los comodines van en el **valor del parámetro**, no pegados al SQL, para no
perder la protección contra inyección.

## Endpoints

| Método | Ruta | Respuesta |
|---|---|---|
| GET | `/productos` | `200` con la lista completa. |
| GET | `/productos?buscar=mo` | `200` con los que coincidan. |
| GET | `/productos/{id}` | `200` con el producto, o `404`. |
| POST | `/productos` | `201` con el producto y su `Id` nuevo, o `400`. |
| PUT | `/productos/{id}` | `204` si se actualizó, `404` si no existe, `400` si el dato viene mal. |
| DELETE | `/productos/{id}` | `204` si se borró, `404` si no existe. |

El `POST` responde `201 Created` e incluye la dirección donde quedó el recurso.
`PUT` y `DELETE` responden `204 No Content` porque la operación salió bien pero
no hay nada que devolver.

## Validación y errores

Son dos niveles distintos.

La **validación** revisa el dato antes de tocar la base: que el nombre no venga
vacío ni pase de 100 caracteres, y que el precio y la cantidad no sean negativos.
Si algo falla responde `400` con el mensaje.

El **manejo de excepciones** atrapa lo que ya no depende de la entrada, como que
la base de datos no esté disponible. Un solo bloque en `Program.cs` cubre toda la
API y responde JSON en lugar de la página de error de ASP.NET:

```csharp
app.UseExceptionHandler(rama => rama.Run(async contexto =>
{
    var error = contexto.Features.Get<IExceptionHandlerFeature>()?.Error;
    contexto.Response.StatusCode = error is SqlException ? 409 : 500;
    await contexto.Response.WriteAsJsonAsync(new { error = error?.Message });
}));
```

Si el error viene de SQL Server responde `409 Conflict`; cualquier otro, `500`.
Así quien consume la API siempre recibe JSON y puede mostrar el mensaje.

## Nota sobre un aviso al compilar

Al compilar salen cuatro advertencias `NU1902` y `NU1903` sobre el paquete
`System.Security.Cryptography.Pkcs`. Viene de adentro de `Microsoft.Data.SqlClient`,
no del código de este proyecto. Son advertencias, no errores.
