-- ============================================================
-- Script 7 – Adicionar coluna ds_email_chefia_1 e ds_email_chefia_2 na tabela s_usuario_sase
-- Executar como owner: psql -U sase_access -d sase_manager -f 07_adicionar_chefia_imediata.sql
-- ============================================================

BEGIN;

ALTER TABLE s_usuario_sase
    ADD COLUMN ds_email_chefia_1 VARCHAR(254) NULL,
    ADD COLUMN ds_email_chefia_2 VARCHAR(254) NULL;

COMMENT ON COLUMN s_usuario_sase.ds_email_chefia_1 IS 'Email da chefia imediata do usuário (obrigatório em novos cadastros)';
COMMENT ON COLUMN s_usuario_sase.ds_email_chefia_2 IS 'Email de uma segunda chefia (opcional)';

COMMIT;