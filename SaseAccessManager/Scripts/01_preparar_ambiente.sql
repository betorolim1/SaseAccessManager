-- ============================================================
-- Script 1 de 2 – Preparação do Ambiente
-- Executar como superusuário: psql -U postgres -f 01_preparar_ambiente.sql
-- ============================================================

-- Desconectar sessões ativas no banco (se existir)
SELECT pg_terminate_backend(pid)
  FROM pg_stat_activity
 WHERE datname = 'sase_manager'
   AND pid <> pg_backend_pid();

-- Dropar banco antigo
DROP DATABASE IF EXISTS sase_manager;

-- Criar usuário owner da aplicação
DROP ROLE IF EXISTS sase_access;
CREATE ROLE sase_access WITH LOGIN PASSWORD 'SaseLocal123';

-- Criar as ROLEs da diretriz MAPA (RG10/RG11)
DROP ROLE IF EXISTS r_sase_access_adm;
DROP ROLE IF EXISTS r_sase_access_apl;
CREATE ROLE r_sase_access_adm;
CREATE ROLE r_sase_access_apl;

-- Associar as ROLEs ao owner
GRANT r_sase_access_adm TO sase_access;
GRANT r_sase_access_apl TO sase_access;

-- Criar o banco com owner dedicado
CREATE DATABASE sase_manager OWNER sase_access;