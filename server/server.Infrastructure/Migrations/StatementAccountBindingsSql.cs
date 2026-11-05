namespace server.Infrastructure.Migrations;

internal static class StatementAccountBindingsSql
{
    public const string Install = """
        CREATE FUNCTION bank_statement_account_binding_guard() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM "Accounts" a WHERE a."Id" = NEW."BankAccountId" AND a."BranchId" = NEW."BranchId" AND a."Type" = 1) THEN
                RAISE EXCEPTION 'OFX identity belongs to a bank account in its branch' USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_BankStatementAccountBindings_Account';
            END IF;
            RETURN NEW;
        END $$;
        CREATE TRIGGER "TR_BankStatementAccountBindings_Coherent" BEFORE INSERT ON "BankStatementAccountBindings"
            FOR EACH ROW EXECUTE FUNCTION bank_statement_account_binding_guard();
        CREATE TRIGGER "TR_BankStatementAccountBindings_Immutable" BEFORE UPDATE OR DELETE ON "BankStatementAccountBindings"
            FOR EACH ROW EXECUTE FUNCTION financial_history_immutable();
        CREATE FUNCTION bank_statement_import_binding_guard() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM "BankStatementAccountBindings" b
                WHERE b."Id" = NEW."AccountBindingId" AND b."BranchId" = NEW."BranchId" AND b."BankAccountId" = NEW."BankAccountId") THEN
                RAISE EXCEPTION 'statement source must match the selected bank binding' USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_BankStatementImports_Binding';
            END IF;
            RETURN NEW;
        END $$;
        CREATE TRIGGER "TR_BankStatementImports_Binding" BEFORE INSERT ON "BankStatementImports"
            FOR EACH ROW EXECUTE FUNCTION bank_statement_import_binding_guard();
        """;
}
