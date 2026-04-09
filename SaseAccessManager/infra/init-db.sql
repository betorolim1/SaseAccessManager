CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);
 
START TRANSACTION;
 
DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260326211547_InitialCreate') THEN
    CREATE TABLE "Users" (
        "Id" text NOT NULL,
        "Email" text NOT NULL,
        "Name" text NOT NULL,
        "LastName" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "ExpiresAt" timestamp with time zone NOT NULL,
        "Status" text NOT NULL,
        "SaseUserId" text,
        "LastRemovalAttempt" timestamp with time zone,
        "ErrorMessage" text,
        "AccessGroups" jsonb NOT NULL,
        CONSTRAINT "PK_Users" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;
 
DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260326211547_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260326211547_InitialCreate', '10.0.5');
    END IF;
END $EF$;
 
COMMIT;
 