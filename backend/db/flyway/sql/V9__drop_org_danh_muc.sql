-- V9: Drop legacy ORG_DANH_MUC table (replaced by CODES + CODE_VALUES)
-- Data was migrated in V8. This table is no longer referenced by the application.

DROP INDEX UX_ORG_DANH_MUC_NHOM_ITEM;
DROP TABLE ORG_DANH_MUC CASCADE CONSTRAINTS;
