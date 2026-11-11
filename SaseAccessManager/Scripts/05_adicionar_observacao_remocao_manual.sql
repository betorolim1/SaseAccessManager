-- ============================================================
-- Script 5 – Adicionar coluna DS_OBSERVACAO_REMOCAO
-- Executar como owner: psql -U sase_access -d sase_manager -f 05_adicionar_observacao_remocao_manual.sql
-- ============================================================

BEGIN;

ALTER TABLE s_usuario_sase
    ADD COLUMN ds_observacao_remocao VARCHAR(500) NULL;

COMMENT ON COLUMN s_usuario_sase.ds_observacao_remocao
    IS 'Observação livre informada na remoção manual do usuário';

COMMIT;