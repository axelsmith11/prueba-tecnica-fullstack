/* =====================================================================
   Prueba técnica - Bonos de desempeño
   Script 2: tabla Bonus. Idempotente (se puede ejecutar más de una vez).
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;   -- requerido por los índices filtrados
GO
USE BonusTest;
GO

IF OBJECT_ID('dbo.Bonus', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Bonus (
        BonusId         INT IDENTITY(1,1) CONSTRAINT PK_Bonus PRIMARY KEY,
        DepartmentCode  CHAR(5)        NOT NULL,
        EmployeeNumber  INT            NOT NULL,
        Period          CHAR(7)        NOT NULL,   -- YYYY-MM
        BonusTypeId     INT            NOT NULL,
        BonusValue      DECIMAL(16,2)  NOT NULL,   -- monto fijo o porcentaje ingresado
        ReasonCode      CHAR(6)        NOT NULL,
        TaxRateApplied  DECIMAL(5,2)   NOT NULL,   -- % de retención del empleado al registrar
        GrossAmount     DECIMAL(16,2)  NOT NULL,
        TaxAmount       DECIMAL(16,2)  NOT NULL,
        NetAmount       DECIMAL(16,2)  NOT NULL,
        BonusStateCode  CHAR(4)        NOT NULL CONSTRAINT DF_Bonus_State DEFAULT ('0201'),
        IsDeleted       BIT            NOT NULL CONSTRAINT DF_Bonus_IsDeleted DEFAULT (0),
        CreatedBy       VARCHAR(50)    NOT NULL,
        CreatedAt       DATETIME2(0)   NOT NULL CONSTRAINT DF_Bonus_CreatedAt DEFAULT (SYSDATETIME()),
        UpdatedBy       VARCHAR(50)    NULL,
        UpdatedAt       DATETIME2(0)   NULL,
        CONSTRAINT FK_Bonus_Employee    FOREIGN KEY (DepartmentCode, EmployeeNumber)
            REFERENCES dbo.Employee (DepartmentCode, EmployeeNumber),
        CONSTRAINT FK_Bonus_BonusType   FOREIGN KEY (BonusTypeId)    REFERENCES dbo.BonusType (BonusTypeId),
        CONSTRAINT FK_Bonus_BonusReason FOREIGN KEY (ReasonCode)     REFERENCES dbo.BonusReason (ReasonCode),
        CONSTRAINT FK_Bonus_State       FOREIGN KEY (BonusStateCode) REFERENCES dbo.State (StateCode),
        CONSTRAINT CK_Bonus_Period      CHECK (Period LIKE '[12][0-9][0-9][0-9]-[01][0-9]'
                                               AND SUBSTRING(Period, 6, 2) BETWEEN '01' AND '12'),
        CONSTRAINT CK_Bonus_BonusValue  CHECK (BonusValue > 0),
        CONSTRAINT CK_Bonus_State       CHECK (BonusStateCode LIKE '02__'),  -- solo estados de bono (no de empleado)
        CONSTRAINT CK_Bonus_TaxRateApplied CHECK (TaxRateApplied BETWEEN 0 AND 100),
        CONSTRAINT CK_Bonus_Amounts     CHECK (GrossAmount > 0 AND TaxAmount >= 0
                                               AND NetAmount = GrossAmount - TaxAmount)
    );
END
GO

-- La tabla puede existir de una ejecución anterior: se agregan los checks nuevos si faltan.
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Bonus_State' AND parent_object_id = OBJECT_ID('dbo.Bonus'))
    ALTER TABLE dbo.Bonus ADD CONSTRAINT CK_Bonus_State CHECK (BonusStateCode LIKE '02__');
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Bonus_TaxRateApplied' AND parent_object_id = OBJECT_ID('dbo.Bonus'))
    ALTER TABLE dbo.Bonus ADD CONSTRAINT CK_Bonus_TaxRateApplied CHECK (TaxRateApplied BETWEEN 0 AND 100);
GO

-- Regla: un solo bono activo (no anulado y no eliminado) por empleado y periodo.
-- Respalda la validación del servicio ante registros concurrentes.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Bonus_ActivePerPeriod' AND object_id = OBJECT_ID('dbo.Bonus'))
    CREATE UNIQUE INDEX UX_Bonus_ActivePerPeriod
        ON dbo.Bonus (DepartmentCode, EmployeeNumber, Period)
        WHERE IsDeleted = 0 AND BonusStateCode <> '0203';
GO

-- Filtros del listado por periodo y estado sobre los registros visibles.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Bonus_Period_State' AND object_id = OBJECT_ID('dbo.Bonus'))
    CREATE INDEX IX_Bonus_Period_State
        ON dbo.Bonus (Period, BonusStateCode)
        WHERE IsDeleted = 0;
GO

-- Listado filtrado por empleado (incluye anulados/pagados): el índice único solo cubre bonos activos.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Bonus_Employee' AND object_id = OBJECT_ID('dbo.Bonus'))
    CREATE INDEX IX_Bonus_Employee ON dbo.Bonus (DepartmentCode, EmployeeNumber, Period) WHERE IsDeleted = 0;
GO
