-- V4: Rename all tables to apply domain-prefix naming convention
-- IDM_ = Identity Management
-- ORG_ = Organization
-- RPT_ = Reporting
-- DOC_ = Document / File storage
-- SYS_ = System / Notification
-- AUD_ = Audit / Log

-- ─── Identity Management ────────────────────────────────────────────────────
RENAME USERS                  TO IDM_USERS;
RENAME ROLES                  TO IDM_ROLES;
RENAME PERMISSIONS            TO IDM_PERMISSIONS;
RENAME ROLE_PERMISSIONS       TO IDM_ROLE_PERMISSIONS;
RENAME USER_ROLE_ASSIGNMENTS  TO IDM_USER_ROLE_ASSIGNMENTS;

-- ─── Organization ───────────────────────────────────────────────────────────
RENAME LOAI_DON_VI            TO ORG_LOAI_DON_VI;
RENAME DON_VI                 TO ORG_DON_VI;

-- ─── Reporting ──────────────────────────────────────────────────────────────
RENAME KY_BAO_CAO             TO RPT_KY_BAO_CAO;
RENAME KY_TRANG_THAI_DON_VI   TO RPT_KY_TRANG_THAI_DON_VI;
RENAME BAO_CAO_SNAPSHOT       TO RPT_BAO_CAO_SNAPSHOT;
RENAME BAO_CAO_FILE           TO RPT_BAO_CAO_FILE;

-- ─── Document ───────────────────────────────────────────────────────────────
RENAME FILE_DINH_KEM          TO DOC_FILE_DINH_KEM;

-- ─── System / Notification ──────────────────────────────────────────────────
RENAME THONG_BAO              TO SYS_THONG_BAO;

-- ─── Audit ──────────────────────────────────────────────────────────────────
RENAME SYSTEM_LOG             TO AUD_SYSTEM_LOG;
