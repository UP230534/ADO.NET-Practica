using Microsoft.Data.SqlClient;

namespace ProductosAPI.Data;

// Fabrica de conexiones ADO.NET.
// Es la unica clase que conoce la cadena de conexion, y la lee de la
// configuracion (appsettings.json / user-secrets), nunca escrita en el codigo.
public class ConexionSql
{
    private readonly string _cadena;

    public ConexionSql(IConfiguration configuration)
    {
        _cadena = configuration.GetConnectionString("Tienda")
                  ?? throw new InvalidOperationException("Falta la cadena de conexion 'Tienda'");
    }

    public SqlConnection Crear() => new SqlConnection(_cadena);
}
