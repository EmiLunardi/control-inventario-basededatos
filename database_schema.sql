-- =========================================================================
-- Modelo Canónico Completo - Control de Inventario y Stock (12 Entidades)
-- Compatible con SQL Server (T-SQL) y Visual Studio (0 Errores)
-- Nota de Seguridad: Las contraseñas se almacenan encriptadas con SHA-256
-- =========================================================================

-- 1. Tabla de Roles (Seguridad RBAC)
CREATE TABLE Roles (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Nombre VARCHAR(50) UNIQUE NOT NULL,
    Descripcion VARCHAR(250) NULL
);
GO

-- 2. Tabla de Permisos (Seguridad RBAC)
CREATE TABLE Permisos (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Nombre VARCHAR(80) UNIQUE NOT NULL,
    Descripcion VARCHAR(250) NULL
);
GO

-- 3. Tabla Intermedia RolesPermisos (Relación N:M)
CREATE TABLE RolesPermisos (
    RolId INT NOT NULL REFERENCES Roles(Id) ON DELETE CASCADE,
    PermisoId INT NOT NULL REFERENCES Permisos(Id) ON DELETE CASCADE,
    PRIMARY KEY (RolId, PermisoId)
);
GO

-- 4. Tabla de Sucursales
CREATE TABLE Sucursales (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Nombre VARCHAR(100) NOT NULL,
    Ubicacion VARCHAR(200) NOT NULL,
    Telefono VARCHAR(50) NULL,
    Activa BIT NOT NULL DEFAULT 1
);
GO

-- 5. Tabla de Usuarios (Con contraseña encriptada mediante SHA-256)
CREATE TABLE Usuarios (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    SucursalId INT NULL REFERENCES Sucursales(Id),
    RolId INT NULL REFERENCES Roles(Id),
    NombreUsuario VARCHAR(50) UNIQUE NOT NULL,
    Email VARCHAR(120) NULL,
    PasswordHash VARCHAR(64) NOT NULL, -- Hash SHA-256 hexadecimal (64 caracteres)
    Rol VARCHAR(30) NOT NULL DEFAULT 'Administrador',
    Activo BIT NOT NULL DEFAULT 1,
    FechaCreacion DATETIME NOT NULL DEFAULT GETDATE()
);
GO

-- 6. Tabla de Categorias
CREATE TABLE Categorias (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Nombre VARCHAR(100) NOT NULL,
    Descripcion VARCHAR(250) NULL,
    Activa BIT NOT NULL DEFAULT 1
);
GO

-- 7. Tabla de Proveedores
CREATE TABLE Proveedores (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    RazonSocial VARCHAR(150) NOT NULL,
    Cuit VARCHAR(20) UNIQUE NOT NULL,
    Telefono VARCHAR(50) NULL,
    Email VARCHAR(120) NULL,
    Direccion VARCHAR(200) NULL,
    Activo BIT NOT NULL DEFAULT 1
);
GO

-- 8. Tabla de Productos
CREATE TABLE Productos (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    CodigoSku VARCHAR(30) UNIQUE NULL,
    CategoriaId INT NULL REFERENCES Categorias(Id),
    ProveedorId INT NULL REFERENCES Proveedores(Id),
    Nombre VARCHAR(150) NOT NULL,
    Descripcion VARCHAR(500) NULL,
    PrecioCosto DECIMAL(10, 2) NOT NULL DEFAULT 0.00,
    Precio DECIMAL(10, 2) NOT NULL,
    StockActual INT NOT NULL DEFAULT 0,
    StockMinimo INT NOT NULL DEFAULT 10,
    Activo BIT NOT NULL DEFAULT 1
);
GO

-- 9. Tabla de StockSucursales (Existencias desglosadas por sucursal y producto)
CREATE TABLE StockSucursales (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ProductoId INT NOT NULL REFERENCES Productos(Id) ON DELETE CASCADE,
    SucursalId INT NOT NULL REFERENCES Sucursales(Id) ON DELETE CASCADE,
    CantidadActual INT NOT NULL DEFAULT 0,
    PuntoReorden INT NOT NULL DEFAULT 10,
    UltimaActualizacion DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT UQ_StockSucursal_Producto_Sucursal UNIQUE (ProductoId, SucursalId)
);
GO

-- 10. Tabla de Movimientos de Stock (Auditoría de Entradas, Salidas y Ajustes)
CREATE TABLE MovimientosStock (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ProductoId INT NOT NULL REFERENCES Productos(Id),
    SucursalId INT NOT NULL REFERENCES Sucursales(Id),
    UsuarioId INT NULL REFERENCES Usuarios(Id),
    TipoMovimiento VARCHAR(20) NOT NULL, -- 'ENTRADA', 'SALIDA', 'AJUSTE'
    Cantidad INT NOT NULL,
    Motivo VARCHAR(250) NULL,
    Fecha DATETIME NOT NULL DEFAULT GETDATE()
);
GO

-- 11. Tabla de OrdenesCompra (Cabecera de pedidos de reposición a proveedores)
CREATE TABLE OrdenesCompra (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    NumeroOrden VARCHAR(30) UNIQUE NOT NULL,
    ProveedorId INT NOT NULL REFERENCES Proveedores(Id),
    SucursalDestinoId INT NOT NULL REFERENCES Sucursales(Id),
    UsuarioId INT NULL REFERENCES Usuarios(Id),
    FechaEmision DATETIME NOT NULL DEFAULT GETDATE(),
    FechaRecepcion DATETIME NULL,
    Estado VARCHAR(20) NOT NULL DEFAULT 'PENDIENTE', -- 'PENDIENTE', 'RECIBIDA', 'CANCELADA'
    Total DECIMAL(12, 2) NOT NULL DEFAULT 0.00,
    Observaciones VARCHAR(500) NULL
);
GO

-- 12. Tabla de DetallesOrdenCompra (Renglones de cada orden de compra)
CREATE TABLE DetallesOrdenCompra (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    OrdenCompraId INT NOT NULL REFERENCES OrdenesCompra(Id) ON DELETE CASCADE,
    ProductoId INT NOT NULL REFERENCES Productos(Id),
    CantidadSolicitada INT NOT NULL,
    PrecioUnitario DECIMAL(10, 2) NOT NULL
);
GO

-- ==========================================
-- Inserción de Datos Iniciales (Mock Data)
-- ==========================================

-- Roles y Permisos
INSERT INTO Roles (Nombre, Descripcion) VALUES
('Administrador', 'Acceso total al sistema y configuración'),
('Gerente Logística', 'Supervisión de stock, proveedores y órdenes de compra'),
('Operador Depósito', 'Registro de entradas y salidas de mercadería por sucursal');

INSERT INTO Permisos (Nombre, Descripcion) VALUES
('VER_DASHBOARD', 'Visualizar tablero de control y semaforización'),
('GESTIONAR_PRODUCTOS', 'Alta, baja y modificación de catálogo de productos'),
('REGISTRAR_MOVIMIENTOS', 'Asentar entradas, salidas y ajustes de stock'),
('EMITIR_ORDENES_COMPRA', 'Gestionar proveedores y emitir órdenes de reposición');

-- Sucursales (Rosario, Santa Fe)
INSERT INTO Sucursales (Nombre, Ubicacion, Telefono) VALUES
('Central', 'Av. Pellegrini 1850, Rosario', '0341-482-1000'),
('Sucursal Norte', 'Av. Alberdi 950, Rosario', '0341-439-2200'),
('Sucursal Sur', 'Av. San Martín 3420, Rosario', '0341-461-3500');

-- Usuarios con Contraseña Encriptada en SHA-256:
-- 'admin123' -> 240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9
INSERT INTO Usuarios (SucursalId, RolId, NombreUsuario, Email, PasswordHash, Rol) 
VALUES (
    1,
    1,
    'admin',
    'admin@distribuidorarosario.com.ar',
    '240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9',
    'Administrador'
);

-- Categorias
INSERT INTO Categorias (Nombre, Descripcion) VALUES
('Bebidas', 'Bebidas alcohólicas y sin alcohol'),
('Almacén', 'Productos secos y no perecederos'),
('Lácteos', 'Productos derivados de la leche refrigerados'),
('Limpieza', 'Artículos de limpieza para el hogar e industria');

-- Proveedores (Rosario y Gran Rosario, Santa Fe)
INSERT INTO Proveedores (RazonSocial, Cuit, Telefono, Email, Direccion) VALUES
('Distribuidora Rosario Bebidas S.A.', '30-54892104-9', '0341-456-8890', 'ventas@rosariobebidas.com.ar', 'Av. Circunvalación y Av. Uriburu, Rosario'),
('Molinos del Litoral S.R.L.', '30-61029384-2', '0341-492-1102', 'pedidos@molinosdellitoral.com.ar', 'Parque Industrial Alvear, Ruta 21 Km 7, Santa Fe'),
('Lácteos Santa Fe S.A.', '30-50129381-7', '0341-431-5228', 'mayoristas@lacteossantafe.com.ar', 'Av. Ovidio Lagos 5200, Rosario'),
('Química San Lorenzo S.A.', '30-70918273-4', '0341-495-9012', 'contacto@quimicasanlorenzo.com.ar', 'Parque Industrial Pérez, Ruta 33 Km 788, Santa Fe');

-- Productos
INSERT INTO Productos (CodigoSku, CategoriaId, ProveedorId, Nombre, PrecioCosto, Precio, StockActual, StockMinimo) VALUES
('SKU-BEB-001', 1, 1, 'Agua Mineral 1.5L', 520.00, 800.00, 150, 50),
('SKU-BEB-002', 1, 1, 'Cerveza Lager 1L', 1650.00, 2500.00, 30, 100),
('SKU-ALM-001', 2, 2, 'Arroz Blanco 1kg', 780.00, 1200.00, 200, 30),
('SKU-ALM-002', 2, 2, 'Fideos Espagueti 500g', 580.00, 900.00, 80, 40),
('SKU-LAC-001', 3, 3, 'Leche Entera 1L', 720.00, 1100.00, 15, 50),
('SKU-LIM-001', 4, 4, 'Lavandina Concentrada 1L', 490.00, 850.00, 120, 20);

-- StockSucursales
INSERT INTO StockSucursales (ProductoId, SucursalId, CantidadActual, PuntoReorden) VALUES
(1, 1, 90, 30),
(1, 2, 60, 20),
(2, 3, 30, 100),
(3, 1, 200, 30),
(5, 1, 15, 50);

-- Movimientos de Stock
INSERT INTO MovimientosStock (ProductoId, SucursalId, UsuarioId, TipoMovimiento, Cantidad, Motivo, Fecha) VALUES
(1, 1, 1, 'ENTRADA', 100, 'Recepción OC-2026-001', DATEADD(DAY, -5, GETDATE())),
(1, 2, 1, 'SALIDA', 20, 'Despacho a cliente mayorista', DATEADD(DAY, -2, GETDATE())),
(2, 1, 1, 'ENTRADA', 50, 'Ingreso inicial', DATEADD(DAY, -10, GETDATE())),
(2, 3, 1, 'SALIDA', 20, 'Despacho Pedido #4412', DATEADD(DAY, -1, GETDATE())),
(3, 1, 1, 'ENTRADA', 200, 'Recepción OC-2026-002', DATEADD(DAY, -15, GETDATE()));

-- Ordenes de Compra y sus Detalles
INSERT INTO OrdenesCompra (NumeroOrden, ProveedorId, SucursalDestinoId, UsuarioId, Estado, Total, Observaciones) VALUES
('OC-2026-001', 1, 1, 1, 'RECIBIDA', 52000.00, 'Abastecimiento mensual bebidas'),
('OC-2026-003', 1, 3, 1, 'PENDIENTE', 198000.00, 'Reposición urgente por stock crítico');

INSERT INTO DetallesOrdenCompra (OrdenCompraId, ProductoId, CantidadSolicitada, PrecioUnitario) VALUES
(1, 1, 100, 520.00),
(2, 2, 120, 1650.00);
GO
