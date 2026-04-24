-- ============================================================
-- Script 2 de 2 – DDL SaseAccessManager
-- Executar como owner: psql -U sase_access -d sase_manager -f 02_criar_estrutura.sql
-- ============================================================
-- Ordem conforme diretriz MAPA/CGSIC:
--   1. CREATE TABLE
--   2. ALTER TABLE (PK)
--   3. CREATE INDEX (se aplicável)
--   4. COMMENT ON TABLE / COLUMN
--   5. GRANT
-- ============================================================

-- Tabela de controle do EF Core (grafia original obrigatória)
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId"    CHARACTER VARYING(150) NOT NULL,
    "ProductVersion"  CHARACTER VARYING(32)  NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM "__EFMigrationsHistory"
        WHERE "MigrationId" = '20260326211547_InitialCreate'
    ) THEN

    -- 1. CREATE TABLE
    CREATE TABLE S_USUARIO_SASE (
        ID_USUARIO_SASE      UUID                     NOT NULL,
        DS_EMAIL             VARCHAR(254)                     NOT NULL,
        NM_USUARIO           VARCHAR(100)                     NOT NULL,
        NM_SOBRENOME         VARCHAR(100),
        DH_CRIACAO           TIMESTAMP WITH TIME ZONE NOT NULL,
        DH_EXPIRACAO         TIMESTAMP WITH TIME ZONE NOT NULL,
        ST_USUARIO           VARCHAR(20)                     NOT NULL,
        ID_USUARIO_PERIMETER VARCHAR(50),
        DH_TENTATIVA_REMOCAO TIMESTAMP WITH TIME ZONE,
        DS_ERRO              TEXT,
        DS_GRUPO_ACESSO      JSONB                    NOT NULL
    );

    -- 2. ALTER TABLE – PK
    ALTER TABLE S_USUARIO_SASE
        ADD CONSTRAINT PK_S_USUARIO_SASE
        PRIMARY KEY (ID_USUARIO_SASE);

    -- 3. CREATE INDEX (reservado para futuras necessidades)

    -- 4. COMMENT ON TABLE / COLUMN
    COMMENT ON TABLE S_USUARIO_SASE
        IS 'Tabela de sistema do SaseAccessManager. Armazena os usuários provisionados no Harmony SASE (Perimeter81).';

    COMMENT ON COLUMN S_USUARIO_SASE.ID_USUARIO_SASE
        IS 'Identificador único do registro (GUID gerado pela aplicação)';

    COMMENT ON COLUMN S_USUARIO_SASE.DS_EMAIL
        IS 'Endereço de e-mail institucional do usuário';

    COMMENT ON COLUMN S_USUARIO_SASE.NM_USUARIO
        IS 'Primeiro nome do usuário';

    COMMENT ON COLUMN S_USUARIO_SASE.NM_SOBRENOME
        IS 'Sobrenome do usuário';

    COMMENT ON COLUMN S_USUARIO_SASE.DH_CRIACAO
        IS 'Data e hora de criação do registro na aplicação';

    COMMENT ON COLUMN S_USUARIO_SASE.DH_EXPIRACAO
        IS 'Data e hora de expiração do acesso SASE';

    COMMENT ON COLUMN S_USUARIO_SASE.ST_USUARIO
        IS 'Status do usuário na plataforma SASE (Active, Expired, Removed etc.)';

    COMMENT ON COLUMN S_USUARIO_SASE.ID_USUARIO_PERIMETER
        IS 'Identificador do usuário retornado pela API do Perimeter81';

    COMMENT ON COLUMN S_USUARIO_SASE.DH_TENTATIVA_REMOCAO
        IS 'Data e hora da última tentativa de remoção via API';

    COMMENT ON COLUMN S_USUARIO_SASE.DS_ERRO
        IS 'Mensagem de erro da última operação malsucedida';

    COMMENT ON COLUMN S_USUARIO_SASE.DS_GRUPO_ACESSO
        IS 'Grupos de acesso atribuídos ao usuário (armazenado em JSONB)';

    -- 5. GRANT
    GRANT SELECT, INSERT, UPDATE, DELETE
        ON S_USUARIO_SASE TO R_SASE_ACCESS_ADM;

    GRANT SELECT, INSERT, UPDATE, DELETE
        ON S_USUARIO_SASE TO R_SASE_ACCESS_APL;

    -- Registro da migração
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260326211547_InitialCreate', '10.0.5');

    END IF;
END $EF$;

COMMIT;