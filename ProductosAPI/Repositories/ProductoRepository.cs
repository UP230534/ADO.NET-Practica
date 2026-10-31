using System.Data;
using Microsoft.Data.SqlClient;
using ProductosAPI.Data;
using ProductosAPI.Models;

namespace ProductosAPI.Repositories;

public class ProductoRepository : IProductoRepository
{
    private const string Columnas = "Id, Nombre, Precio, Cantidad";

    private readonly ConexionSql _conexion;

    public ProductoRepository(ConexionSql conexion) => _conexion = conexion;

    // Consulta de varias filas con DataReader: se lee hacia adelante,
    // una fila a la vez, sin guardar toda la tabla en memoria.
    public List<Producto> ObtenerTodos()
    {
        var lista = new List<Producto>();

        using var cn = _conexion.Crear();
        cn.Open();

        using var cmd = new SqlCommand($"SELECT {Columnas} FROM Producto ORDER BY Nombre", cn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) lista.Add(Mapear(reader));

        return lista;
    }

    public Producto? ObtenerPorId(int id)
    {
        using var cn = _conexion.Crear();
        cn.Open();

        using var cmd = new SqlCommand($"SELECT {Columnas} FROM Producto WHERE Id = @Id", cn);
        cmd.Parameters.AddWithValue("@Id", id);

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? Mapear(reader) : null;
    }

    // Busqueda con DataAdapter + DataTable: el adaptador abre la conexion,
    // llena la tabla en memoria y la cierra solo.
    public List<Producto> Buscar(string texto)
    {
        using var cn = _conexion.Crear();

        using var cmd = new SqlCommand(
            $"SELECT {Columnas} FROM Producto WHERE Nombre LIKE @Texto ORDER BY Nombre", cn);
        cmd.Parameters.AddWithValue("@Texto", $"%{texto}%");

        var tabla = new DataTable();
        using var adaptador = new SqlDataAdapter(cmd);
        adaptador.Fill(tabla);

        var lista = new List<Producto>();
        foreach (DataRow fila in tabla.Rows)
        {
            lista.Add(new Producto
            {
                Id = (int)fila["Id"],
                Nombre = (string)fila["Nombre"],
                Precio = (decimal)fila["Precio"],
                Cantidad = (int)fila["Cantidad"],
            });
        }

        return lista;
    }

    public int Insertar(Producto producto)
    {
        using var cn = _conexion.Crear();
        cn.Open();

        using var cmd = new SqlCommand(
            @"INSERT INTO Producto (Nombre, Precio, Cantidad)
              VALUES (@Nombre, @Precio, @Cantidad);
              SELECT CAST(SCOPE_IDENTITY() AS INT);", cn);
        AgregarParametros(cmd, producto);

        // ExecuteScalar devuelve el primer dato de la primera fila: el Id nuevo
        return (int)cmd.ExecuteScalar()!;
    }

    public bool Actualizar(Producto producto)
    {
        using var cn = _conexion.Crear();
        cn.Open();

        using var cmd = new SqlCommand(
            @"UPDATE Producto SET Nombre = @Nombre, Precio = @Precio, Cantidad = @Cantidad
              WHERE Id = @Id", cn);
        cmd.Parameters.AddWithValue("@Id", producto.Id);
        AgregarParametros(cmd, producto);

        // ExecuteNonQuery devuelve cuantas filas cambiaron: 0 = no existia
        return cmd.ExecuteNonQuery() > 0;
    }

    public bool Eliminar(int id)
    {
        using var cn = _conexion.Crear();
        cn.Open();

        using var cmd = new SqlCommand("DELETE FROM Producto WHERE Id = @Id", cn);
        cmd.Parameters.AddWithValue("@Id", id);

        return cmd.ExecuteNonQuery() > 0;
    }

    // Convierte una fila del DataReader en un objeto Producto
    static Producto Mapear(SqlDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        Nombre = reader.GetString(1),
        Precio = reader.GetDecimal(2),
        Cantidad = reader.GetInt32(3),
    };

    static void AgregarParametros(SqlCommand cmd, Producto producto)
    {
        cmd.Parameters.AddWithValue("@Nombre", producto.Nombre);
        cmd.Parameters.AddWithValue("@Precio", producto.Precio);
        cmd.Parameters.AddWithValue("@Cantidad", producto.Cantidad);
    }
}
