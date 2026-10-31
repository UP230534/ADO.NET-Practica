-- Base de datos de la tienda en linea.
-- Se ejecuta una sola vez, antes de encender la API.

IF DB_ID('TiendaDb') IS NULL
    CREATE DATABASE TiendaDb;
GO

USE TiendaDb;
GO

DROP TABLE IF EXISTS Producto;
GO

CREATE TABLE Producto (
    Id       INT IDENTITY(1,1) PRIMARY KEY,
    Nombre   NVARCHAR(100) NOT NULL,
    Precio   DECIMAL(10,2) NOT NULL,
    Cantidad INT           NOT NULL
);
GO

INSERT INTO Producto (Nombre, Precio, Cantidad) VALUES
    ('Teclado mecanico', 899.00, 12),
    ('Mouse inalambrico', 349.50, 30),
    ('Monitor 24 pulgadas', 3200.00, 5),
    ('Audifonos bluetooth', 1250.00, 18);
GO
