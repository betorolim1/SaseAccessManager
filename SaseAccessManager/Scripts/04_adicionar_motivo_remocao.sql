-- ============================================================
-- Script 4 – Adicionar coluna DS_MOTIVO_REMOCAO
-- Executar como owner: psql -U sase_access -d sase_manager -f 04_adicionar_motivo_remocao.sql
-- ============================================================

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 's_usuario_sase'
          AND column_name = 'ds_motivo_remocao'
    ) THEN

    ALTER TABLE S_USUARIO_SASE
        ADD COLUMN DS_MOTIVO_REMOCAO TEXT;

    COMMENT ON COLUMN S_USUARIO_SASE.DS_MOTIVO_REMOCAO
        IS 'Motivo da remoção do acesso';

    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260619000000_AddMotivoRemocao', '10.0.5');

    END IF;
END $EF$;

COMMIT;