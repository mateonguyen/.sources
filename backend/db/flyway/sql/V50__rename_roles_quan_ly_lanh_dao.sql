-- V50: Doi ten vai tro H05_ADMIN -> QUAN_LY, H05_VIEWER -> LANH_DAO
-- Ly do: ten cu gan voi ten to chuc cu the (H05), can ten trung lap hon

UPDATE IDM_ROLES
SET NAME            = 'QUAN_LY',
    NORMALIZED_NAME = 'QUAN_LY',
    TEN_ROLE        = 'Quan ly'
WHERE NORMALIZED_NAME = 'H05_ADMIN';

UPDATE IDM_ROLES
SET NAME            = 'LANH_DAO',
    NORMALIZED_NAME = 'LANH_DAO',
    TEN_ROLE        = 'Lanh dao'
WHERE NORMALIZED_NAME = 'H05_VIEWER';
