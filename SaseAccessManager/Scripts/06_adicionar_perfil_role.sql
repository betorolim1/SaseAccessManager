-- ============================================================
-- Script 6 – Adicionar coluna DS_PERFIL_SASE
-- Executar como owner: psql -U sase_access -d sase_manager -f 06_adicionar_perfil_role.sql
-- ============================================================

BEGIN;

ALTER TABLE s_usuario_sase
    ADD COLUMN ds_perfil_sase VARCHAR(20) NULL;

COMMENT ON COLUMN s_usuario_sase.ds_perfil_sase
    IS 'Perfil do usuário no Harmony SASE (Admin, User). Atualizado pela sincronização';

COMMIT;