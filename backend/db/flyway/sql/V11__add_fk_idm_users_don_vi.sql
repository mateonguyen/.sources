-- Add foreign key IDM_USERS.DON_VI_ID -> ORG_DON_VI.ID safely

DECLARE
    v_invalid_count NUMBER;
BEGIN
    SELECT COUNT(1)
      INTO v_invalid_count
      FROM IDM_USERS u
      LEFT JOIN ORG_DON_VI d ON d.ID = u.DON_VI_ID
     WHERE u.DON_VI_ID IS NULL
        OR u.DON_VI_ID <= 0
        OR d.ID IS NULL;

    IF v_invalid_count > 0 THEN
        RAISE_APPLICATION_ERROR(
            -20001,
            'Cannot add FK IDM_USERS.DON_VI_ID -> ORG_DON_VI.ID because invalid data exists. Invalid rows: ' || v_invalid_count
        );
    END IF;
END;
/

CREATE INDEX IX_IDM_USERS_DON_VI_ID ON IDM_USERS (DON_VI_ID);

ALTER TABLE IDM_USERS
    ADD CONSTRAINT FK_IDM_USERS_DONVI
    FOREIGN KEY (DON_VI_ID)
    REFERENCES ORG_DON_VI(ID);
