using ProductosAPI.Models;
using ProductosAPI.Repositories;

namespace ProductosAPI.Endpoints;

public static class ProductosEndpoints
{
    public static void MapProductoEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/productos").WithTags("Productos");

        // GET /productos            todos
        // GET /productos?buscar=te  los que tengan "te" en el nombre
        group.MapGet("/", (IProductoRepository repo, string? buscar) =>
            Results.Ok(string.IsNullOrWhiteSpace(buscar)
                ? repo.ObtenerTodos()
                : repo.Buscar(buscar)));

        group.MapGet("/{id:int}", (IProductoRepository repo, int id) =>
        {
            var producto = repo.ObtenerPorId(id);
            return producto is null
                ? Results.NotFound(new { mensaje = "Producto no encontrado" })
                : Results.Ok(producto);
        });

        group.MapPost("/", (IProductoRepository repo, Producto nuevo) =>
        {
            var error = Validar(nuevo);
            if (error is not null) return Results.BadRequest(new { error });

            nuevo.Id = repo.Insertar(nuevo);
            return Results.Created($"/productos/{nuevo.Id}", nuevo);
        });

        group.MapPut("/{id:int}", (IProductoRepository repo, int id, Producto actualizado) =>
        {
            var error = Validar(actualizado);
            if (error is not null) return Results.BadRequest(new { error });

            actualizado.Id = id;
            return repo.Actualizar(actualizado)
                ? Results.NoContent()
                : Results.NotFound(new { mensaje = "Producto no encontrado" });
        });

        group.MapDelete("/{id:int}", (IProductoRepository repo, int id) =>
            repo.Eliminar(id)
                ? Results.NoContent()
                : Results.NotFound(new { mensaje = "Producto no encontrado" }));
    }

    static string? Validar(Producto producto)
    {
        if (string.IsNullOrWhiteSpace(producto.Nombre)) return "El nombre es obligatorio";
        if (producto.Nombre.Length > 100) return "El nombre no puede superar 100 caracteres";
        if (producto.Precio < 0) return "El precio no puede ser negativo";
        if (producto.Cantidad < 0) return "La cantidad no puede ser negativa";
        return null;
    }
}
