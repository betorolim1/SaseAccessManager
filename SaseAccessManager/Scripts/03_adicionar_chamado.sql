-- ============================================================
-- Script 3 – Adicionar coluna DS_CHAMADO
-- Executar como owner: psql -U sase_access -d sase_manager -f 03_adicionar_chamado.sql
-- ============================================================

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 's_usuario_sase'
          AND column_name = 'ds_chamado'
    ) THEN

    ALTER TABLE S_USUARIO_SASE
        ADD COLUMN DS_CHAMADO TEXT;

    COMMENT ON COLUMN S_USUARIO_SASE.DS_CHAMADO
        IS 'Número ou descrição do chamado que originou a solicitação de acesso';

    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260618000000_AddChamado', '10.0.5');

    END IF;
END $EF$;

COMMIT;