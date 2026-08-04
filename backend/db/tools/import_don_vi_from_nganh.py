"""
Import 1 lan: T_AD_ORGANIZE (DB nganh, C##VBDH) -> REF_DON_VI (DB he thong nay, CAND_QLCNTT).

CHAY 1 LAN DUY NHAT tren du lieu demo. Script se:
  1. Tu dong do TOAN BO bang co FK tro vao REF_DON_VI (khong can liet ke tay).
  2. Xoa sach du lieu demo o cac bang do (tru IDM_USERS - xu ly rieng, xem duoi).
  3. Tam tat FK cua IDM_USERS -> REF_DON_VI (vi DON_VI_ID la NOT NULL, dung de
     dang nhap - KHONG xoa user), xoa REF_DON_VI demo, import du lieu moi.
  4. Gan lai TOAN BO IDM_USERS.DON_VI_ID ve don vi goc G01 vua import (de tai
     khoan van dang nhap duoc), roi bat lai FK.
     => Sau khi chay xong, ban can vao UI gan lai dung don vi cho tung user demo
        (ngoai admin) neu ho khong thuoc thang G01.

Yeu cau: pip install oracledb

Cach dung:
    python import_don_vi_from_nganh.py --dry-run   # chi doc + in ra, KHONG ghi gi vao DB2
    python import_don_vi_from_nganh.py              # chay that

Chi lay cac don vi thuoc Bo Cong an: ID = 'G01' hoac ID LIKE 'G01.%'
(loai cac don vi ma khac, vd G00 - UBND cac tinh...).

Mapping:
    T_AD_ORGANIZE.ID          -> REF_DON_VI.MA_DON_VI   (giu nguyen ma nganh, vd G01.101.000.000)
    T_AD_ORGANIZE.PID         -> REF_DON_VI.PARENT_ID    (resolve qua map MA_DON_VI -> ID moi, 2 pha)
    T_AD_ORGANIZE.NAME        -> REF_DON_VI.TEN_DON_VI
    T_AD_ORGANIZE.SHORT_NAME  -> REF_DON_VI.TEN_VIET_TAT
    T_AD_ORGANIZE.IS_ACTIVE   -> REF_DON_VI.IS_ACTIVE (mac dinh 1 neu null)
    T_AD_ORGANIZE.IS_DELETED=1 -> BI LOAI, khong import

Cac cot REF_DON_VI khong co nguon tu DB nganh (DIA_CHI, CAP_DON_VI,
WEBSITE_NOI_BO, WEBSITE_INTERNET, TONG_BIEN_CHE) -> de NULL, nhap tay sau.
"""

import argparse
import sys

import oracledb

DB1_DSN = dict(
    user="C##VBDH",
    password="vbdh123",
    dsn="(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=localhost)(PORT=1521))"
    "(CONNECT_DATA=(SERVICE_NAME=XE)))",
)

DB2_DSN = dict(
    user="CAND_QLCNTT",
    password="123456",
    dsn="localhost:1521/XEPDB1",
)

# Chi lay cac don vi thuoc Bo Cong an (ma goc G01 + toan bo con chau).
ROOT_MA_DON_VI = "G01"

# Bang chua tai khoan dang nhap - KHONG xoa du lieu, chi tam tat FK roi gan lai
# DON_VI_ID sau khi import xong (vi cot nay NOT NULL, khong the de trong).
USERS_TABLE = "IDM_USERS"


def fetch_source_rows(conn1) -> list[dict]:
    cur = conn1.cursor()
    cur.execute(
        """
        SELECT ID, PID, NAME, SHORT_NAME, IS_ACTIVE
        FROM T_AD_ORGANIZE
        WHERE (IS_DELETED = 0 OR IS_DELETED IS NULL)
          AND (ID = :root OR ID LIKE :root_prefix)
        ORDER BY ID
        """,
        {"root": ROOT_MA_DON_VI, "root_prefix": f"{ROOT_MA_DON_VI}.%"},
    )
    cols = [d[0] for d in cur.description]
    rows = [dict(zip(cols, r)) for r in cur.fetchall()]
    cur.close()
    return rows


def find_all_fk_edges(cur) -> list[tuple[str, str]]:
    """Tra ve toan bo canh FK trong schema: (child_table, parent_table)."""
    cur.execute(
        """
        SELECT uc.table_name AS child_table, r.table_name AS parent_table
        FROM user_constraints uc
        JOIN user_constraints r ON r.constraint_name = uc.r_constraint_name
                                AND r.owner = uc.owner
        WHERE uc.constraint_type = 'R'
        """
    )
    return cur.fetchall()


def transitive_dependents(edges: list[tuple[str, str]], root: str) -> set[str]:
    """BFS nguoc theo canh FK: tim TAT CA bang phu thuoc (truc tiep + gian tiep,
    qua nhieu cap) vao `root`. Vd A->REF_DON_VI, B->A thi B cung nam trong ket qua."""
    parent_to_children: dict[str, list[str]] = {}
    for child, parent in edges:
        parent_to_children.setdefault(parent, []).append(child)

    visited: set[str] = set()
    queue = [root]
    while queue:
        cur_parent = queue.pop()
        for child in parent_to_children.get(cur_parent, []):
            if child != root and child not in visited:
                visited.add(child)
                queue.append(child)
    return visited


def wipe_dependent_tables(conn2, edges: list[tuple[str, str]]):
    """Xoa sach du lieu TAT CA bang phu thuoc (truc tiep/gian tiep) vao REF_DON_VI
    (tru USERS_TABLE, xu ly rieng). Retry nhieu vong de tu giai quyet thu tu phu
    thuoc giua cac bang con voi nhau (vd BIZ_CAMERA_THUC_TRANG -> BIZ_CAMERA_QUAN_LY,
    RPT_SNAPSHOT_BATCH -> RPT_BAO_CAO_SNAPSHOT -> REF_DON_VI)."""
    cur = conn2.cursor()
    dependents = transitive_dependents(edges, "REF_DON_VI")
    pending = sorted(t for t in dependents if t != USERS_TABLE)
    print(f"  Bang se xoa (truc tiep + gian tiep): {pending}")

    max_rounds = len(pending) + 2
    for round_no in range(max_rounds):
        if not pending:
            break
        still_pending = []
        for tbl in pending:
            try:
                cur.execute(f"DELETE FROM {tbl}")
                conn2.commit()
                print(f"  OK  DELETE FROM {tbl} ({cur.rowcount} dong)")
            except oracledb.IntegrityError:
                conn2.rollback()
                still_pending.append(tbl)
        pending = still_pending

    cur.close()
    if pending:
        raise RuntimeError(
            f"Khong the xoa cac bang sau do vuong FK lan nhau, kiem tra tay: {pending}"
        )


def disable_users_fk(conn2) -> str:
    cur = conn2.cursor()
    cur.execute(
        """
        SELECT constraint_name FROM user_constraints
        WHERE table_name = :t AND constraint_type = 'R'
          AND r_constraint_name = (
              SELECT constraint_name FROM user_constraints
              WHERE table_name = 'REF_DON_VI' AND constraint_type = 'P'
          )
        """,
        {"t": USERS_TABLE},
    )
    row = cur.fetchone()
    if not row:
        cur.close()
        raise RuntimeError(f"Khong tim thay FK cua {USERS_TABLE} -> REF_DON_VI")
    fk_name = row[0]
    print(f"  Tam tat FK {fk_name} tren {USERS_TABLE} ...")
    cur.execute(f"ALTER TABLE {USERS_TABLE} DISABLE CONSTRAINT {fk_name}")
    conn2.commit()
    cur.close()
    return fk_name


def enable_users_fk(conn2, fk_name: str):
    cur = conn2.cursor()
    print(f"  Bat lai FK {fk_name} tren {USERS_TABLE} ...")
    cur.execute(f"ALTER TABLE {USERS_TABLE} ENABLE CONSTRAINT {fk_name}")
    conn2.commit()
    cur.close()


def wipe_ref_don_vi(conn2):
    cur = conn2.cursor()
    print("  DELETE FROM REF_DON_VI ...")
    # Xoa theo 2 buoc de tranh vuong self-referencing FK PARENT_ID
    cur.execute("UPDATE REF_DON_VI SET PARENT_ID = NULL")
    cur.execute("DELETE FROM REF_DON_VI")
    conn2.commit()
    cur.close()


def import_pass1_insert_flat(conn2, rows: list[dict]) -> dict[str, int]:
    """Insert phang, PARENT_ID de NULL. Tra ve map MA_DON_VI(DB1.ID) -> ID moi (local)."""
    cur = conn2.cursor()
    id_map: dict[str, int] = {}

    for row in rows:
        ma_don_vi = row["ID"]
        ten = row["NAME"] or ma_don_vi
        ten_viet_tat = row["SHORT_NAME"]
        is_active = 1 if row["IS_ACTIVE"] in (None, 1) else 0

        new_id_var = cur.var(int)
        cur.execute(
            """
            INSERT INTO REF_DON_VI
                (MA_DON_VI, TEN_DON_VI, TEN_VIET_TAT, IS_ACTIVE, PARENT_ID,
                 CREATED_AT)
            VALUES
                (:ma_don_vi, :ten_don_vi, :ten_viet_tat, :is_active, NULL,
                 SYSTIMESTAMP)
            RETURNING ID INTO :new_id
            """,
            {
                "ma_don_vi": ma_don_vi,
                "ten_don_vi": ten,
                "ten_viet_tat": ten_viet_tat,
                "is_active": is_active,
                "new_id": new_id_var,
            },
        )
        id_map[ma_don_vi] = int(new_id_var.getvalue()[0])

    conn2.commit()
    cur.close()
    return id_map


def import_pass2_set_parent(conn2, rows: list[dict], id_map: dict[str, int]) -> list[str]:
    """Gan PARENT_ID qua id_map. Tra ve danh sach MA_DON_VI co PID mo coi (khong tim thay)."""
    cur = conn2.cursor()
    orphans: list[str] = []

    for row in rows:
        pid = row["PID"]
        if not pid:
            continue
        child_local_id = id_map.get(row["ID"])
        parent_local_id = id_map.get(pid)
        if parent_local_id is None:
            orphans.append(row["ID"])
            continue
        cur.execute(
            "UPDATE REF_DON_VI SET PARENT_ID = :parent_id WHERE ID = :child_id",
            {"parent_id": parent_local_id, "child_id": child_local_id},
        )

    conn2.commit()
    cur.close()
    return orphans


def remap_users_to_root(conn2, root_id: int) -> int:
    cur = conn2.cursor()
    cur.execute(
        f"UPDATE {USERS_TABLE} SET DON_VI_ID = :root_id",
        {"root_id": root_id},
    )
    n = cur.rowcount
    conn2.commit()
    cur.close()
    return n


def validate(conn2, rows: list[dict], id_map: dict[str, int], orphans: list[str]):
    cur = conn2.cursor()
    cur.execute("SELECT COUNT(*) FROM REF_DON_VI")
    total_imported = cur.fetchone()[0]

    cur.execute("SELECT COUNT(*) FROM REF_DON_VI WHERE PARENT_ID IS NULL")
    root_count_db2 = cur.fetchone()[0]

    cur.execute(
        """
        SELECT MA_DON_VI, COUNT(*) FROM REF_DON_VI
        GROUP BY MA_DON_VI HAVING COUNT(*) > 1
        """
    )
    dup_ma_don_vi = cur.fetchall()
    cur.close()

    root_count_db1 = sum(1 for r in rows if not r["PID"])

    print("\n=== VALIDATE ===")
    print(f"So dong nguon (T_AD_ORGANIZE, da loc G01):  {len(rows)}")
    print(f"So dong import vao REF_DON_VI:               {total_imported}")
    print(f"So node goc (PID null) - nguon:               {root_count_db1}")
    print(f"So node goc (PARENT_ID null) - dich:           {root_count_db2}")
    print(f"MA_DON_VI trung lap:                           {dup_ma_don_vi or 'khong co'}")
    print(f"PID mo coi (khong tim thay parent):            {orphans or 'khong co'}")

    ok = (
        total_imported == len(rows)
        and root_count_db1 == root_count_db2
        and not dup_ma_don_vi
        and not orphans
    )
    print("=> " + ("OK" if ok else "CO VAN DE, kiem tra lai truoc khi dung du lieu"))
    return ok


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--dry-run", action="store_true", help="Chi doc va in ra, khong ghi vao DB2")
    args = parser.parse_args()

    print("Ket noi DB1 (T_AD_ORGANIZE) ...")
    conn1 = oracledb.connect(**DB1_DSN)
    print("Ket noi DB2 (REF_DON_VI) ...")
    conn2 = oracledb.connect(**DB2_DSN)

    try:
        rows = fetch_source_rows(conn1)
        print(f"Doc duoc {len(rows)} dong tu T_AD_ORGANIZE (thuoc G01, chua bi xoa).")

        if args.dry_run:
            print("\n--dry-run: khong ghi gi vao DB2. 5 dong mau:")
            for r in rows[:5]:
                print(" ", r)
            return

        cur2 = conn2.cursor()
        edges = find_all_fk_edges(cur2)
        cur2.close()

        print("\nXoa du lieu demo o cac bang phu thuoc (tru IDM_USERS)...")
        wipe_dependent_tables(conn2, edges)

        fk_name = disable_users_fk(conn2)
        id_map: dict[str, int] = {}
        orphans: list[str] = []
        try:
            print("\nDon REF_DON_VI hien tai (demo)...")
            wipe_ref_don_vi(conn2)

            print("Pha 1: insert phang...")
            id_map = import_pass1_insert_flat(conn2, rows)

            print("Pha 2: gan PARENT_ID...")
            orphans = import_pass2_set_parent(conn2, rows, id_map)

            # Gan lai user VE DON VI MOI truoc khi bat lai FK, neu khong FK se
            # khong the validate (user van tro toi ID cu da bi xoa).
            root_id = id_map.get(ROOT_MA_DON_VI)
            if root_id is not None:
                n = remap_users_to_root(conn2, root_id)
                print(f"\nDa gan lai {n} tai khoan (IDM_USERS) ve don vi goc G01 (ID={root_id}).")
                print("=> Vao UI gan lai DUNG don vi cho tung user demo neu can (tru admin o goc).")
            else:
                print("\nCANH BAO: khong tim thay ID moi cua G01, chua remap IDM_USERS.DON_VI_ID.")
                print("FK se khong bat lai duoc neu con user tro toi ID cu.")
        finally:
            enable_users_fk(conn2, fk_name)

        ok = validate(conn2, rows, id_map, orphans)
        sys.exit(0 if ok else 1)
    finally:
        conn1.close()
        conn2.close()


if __name__ == "__main__":
    main()
