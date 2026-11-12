/* =====================================================================
   Prueba técnica - Bonos de desempeño
   Script 3: detalle del pago de un bono distribuido por proyecto (punto extra 1).
   Idempotente.
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
USE BonusTest;
GO

IF OBJECT_ID('dbo.BonusPaymentDetail', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.BonusPaymentDetail (
        BonusPaymentDetailId INT IDENTITY(1,1) CONSTRAINT PK_BonusPaymentDetail PRIMARY KEY,
        BonusId      INT            NOT NULL CONSTRAINT FK_BonusPaymentDetail_Bonus REFERENCES dbo.Bonus (BonusId),
        ProjectName  VARCHAR(100)   NOT NULL,
        Hours        DECIMAL(8,2)   NOT NULL CONSTRAINT CK_BonusPaymentDetail_Hours CHECK (Hours > 0),
        Amount       DECIMAL(16,2)  NOT NULL,
        CreatedBy    VARCHAR(50)    NOT NULL,
        CreatedAt    DATETIME2(0)   NOT NULL CONSTRAINT DF_BonusPaymentDetail_CreatedAt DEFAULT (SYSDATETIME())
    );
END
GO

-- Consulta del detalle por bono.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BonusPaymentDetail_BonusId' AND object_id = OBJECT_ID('dbo.BonusPaymentDetail'))
    CREATE INDEX IX_BonusPaymentDetail_BonusId ON dbo.BonusPaymentDetail (BonusId);
GO
