/* =====================================================================
   Prueba técnica - Bonos de desempeño
   Script 1: tablas existentes y datos semilla. NO MODIFICAR.
   El candidato agrega sus propios scripts a partir de 02_*.sql
   ===================================================================== */

IF DB_ID('BonusTest') IS NULL
    CREATE DATABASE BonusTest;
GO
USE BonusTest;
GO

CREATE TABLE dbo.State (
    StateCode        CHAR(4)      PRIMARY KEY,
    StateDescription VARCHAR(50)  NOT NULL,
    MasterStateCode  CHAR(2)      NOT NULL   -- 01 = empleado, 02 = bono
);

CREATE TABLE dbo.Department (
    DepartmentCode CHAR(5)       PRIMARY KEY,
    DepartmentName VARCHAR(100)  NOT NULL
);

CREATE TABLE dbo.Employee (
    DepartmentCode    CHAR(5)        NOT NULL REFERENCES dbo.Department(DepartmentCode),
    EmployeeNumber    INT            NOT NULL,
    FullName          VARCHAR(150)   NOT NULL,
    HireDate          DATE           NOT NULL,
    MonthlySalary     DECIMAL(16,2)  NOT NULL,
    TaxRate           DECIMAL(5,2)   NOT NULL,  -- % de retención sobre sus bonos
    EmployeeStateCode CHAR(4)        NOT NULL REFERENCES dbo.State(StateCode),
    CONSTRAINT PK_Employee PRIMARY KEY (DepartmentCode, EmployeeNumber)
);

CREATE TABLE dbo.EmployeeProjectHours (
    EmployeeProjectHoursId INT IDENTITY(1,1) PRIMARY KEY,
    DepartmentCode         CHAR(5)       NOT NULL,
    EmployeeNumber         INT           NOT NULL,
    ProjectName            VARCHAR(100)  NOT NULL,
    Hours                  DECIMAL(8,2)  NOT NULL,  -- horas del último mes
    CONSTRAINT FK_EmployeeProjectHours_Employee FOREIGN KEY (DepartmentCode, EmployeeNumber)
        REFERENCES dbo.Employee(DepartmentCode, EmployeeNumber)
);

CREATE TABLE dbo.BonusType (
    BonusTypeId          INT          PRIMARY KEY,
    BonusTypeDescription VARCHAR(30)  NOT NULL
);

CREATE TABLE dbo.BonusReason (
    ReasonCode        CHAR(6)       PRIMARY KEY,
    ReasonDescription VARCHAR(100)  NOT NULL,
    State             BIT           NOT NULL
);
GO

INSERT INTO dbo.State VALUES
 ('0101','Activo','01'), ('0102','Cesado','01'),
 ('0201','Registrado','02'), ('0202','Pagado','02'), ('0203','Anulado','02');

INSERT INTO dbo.Department VALUES ('TECH1','Tecnología'), ('VENTA','Ventas');

INSERT INTO dbo.BonusType VALUES (1,'Monto fijo'), (2,'Porcentaje del sueldo');

INSERT INTO dbo.BonusReason VALUES
 ('000001','Cumplimiento de metas',1),
 ('000002','Proyecto entregado antes de plazo',1),
 ('000003','Reconocimiento del cliente',1),
 ('000004','Motivo descontinuado',0);

INSERT INTO dbo.Employee VALUES
 ('TECH1',101,'Andrea Torres Vega','2022-03-01',5000.00, 8.00,'0101'),
 ('TECH1',102,'Carlos Ruiz Medina','2023-07-15',3000.00, 8.00,'0101'),
 ('TECH1',103,'Lucía Flores Paz',  '2021-01-10',8000.00,10.00,'0101'),
 ('VENTA',201,'Jorge Salas León',  '2024-02-01',2500.00, 0.00,'0101'),
 ('VENTA',202,'Rosa Medina Quispe','2020-05-20',2000.00, 8.00,'0102');

INSERT INTO dbo.EmployeeProjectHours (DepartmentCode,EmployeeNumber,ProjectName,Hours) VALUES
 ('TECH1',101,'Portal de clientes', 120),
 ('TECH1',101,'App móvil',           40),
 ('TECH1',102,'Portal de clientes',  45),
 ('TECH1',102,'App móvil',           35),
 ('TECH1',102,'Soporte',             20),
 ('TECH1',103,'Data warehouse',      30),
 ('TECH1',103,'Portal de clientes',  50),
 ('TECH1',103,'Migración a la nube', 20),
 ('VENTA',201,'Campaña de verano',  160),
 ('VENTA',202,'Campaña de verano',  100);
GO
