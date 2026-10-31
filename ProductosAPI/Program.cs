using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
using ProductosAPI.Data;
using ProductosAPI.Endpoints;
using ProductosAPI.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Inyeccion de dependencias: los endpoints piden la interfaz, no la clase
builder.Services.AddSingleton<ConexionSql>();
builder.Services.AddScoped<IProductoRepository, ProductoRepository>();

var app = builder.Build();

// Manejo de excepciones: si falla la base de datos el cliente recibe un JSON
// con el error, no la pagina de error de ASP.NET
app.UseExceptionHandler(rama => rama.Run(async contexto =>
{
    var error = contexto.Features.Get<IExceptionHandlerFeature>()?.Error;
    contexto.Response.StatusCode = error is SqlException ? 409 : 500;
    await contexto.Response.WriteAsJsonAsync(new { error = error?.Message });
}));

app.UseSwagger();
app.UseSwaggerUI();

app.MapProductoEndpoints();

app.Run();
