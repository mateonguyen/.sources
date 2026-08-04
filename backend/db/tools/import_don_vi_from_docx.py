"""
Import 1 lan: 4 file docx (Phu luc 1 - cap 2, Phu luc cap 3.1/3.2/3.3) theo
Thong tu 58/2025/TT-BCA -> REF_DON_VI (DB he thong nay, CAND_QLCNTT).

THAY THE hoan toan du lieu REF_DON_VI hien co (ke ca dot import tu
T_AD_ORGANIZE truoc do) - vi khac dinh dang ma (3 doan vs 4 doan) va bo docx
la nguon co can cu phap ly moi hon, day du hon.

Yeu cau: pip install oracledb python-docx

Cach dung:
    python import_don_vi_from_docx.py --input-dir "C:\\duong_dan_chua_4_file" --dry-run
    python import_don_vi_from_docx.py --input-dir "C:\\duong_dan_chua_4_file"

--input-dir: thu muc chua dung 4 file goc (ten file khong quan trong, script tu
nhan dien qua noi dung "3.1"/"3.2"/"3.3" trong ten file, file con lai la cap 2).
Mac dinh: cung thu muc voi script.

=== Cau truc & quy tac xu ly ===

Cap 2 (Phu luc 1): 111 dong, khong trung ma. La goc + cac don vi truc thuoc Bo
(gom ca 34 Cong an tinh/thanh). Dong dau (G01.000.000 - Bo Cong an) la goc,
cac dong con lai la con truc tiep cua goc.

Cap 3.1: 878 dong, phong thuoc cac don vi cap 2 (KHONG phai tinh). Cac dong co
ma trung voi cap 2 la dong "nhac lai don vi cha" de de doc - BI LOAI, chi lay
dong con thuc su, gan cha = dong cap-2 gan nhat truoc do trong bang.

Cap 3.2: 37 dong, la TEMPLATE (ma co "xxx" thay cho ma tinh) cho cac phong
thuoc Cong an tinh/thanh. Duoc ap dung cho 34 tinh/thanh (lay tu cap 2, cac
dong ten bat dau "Cong an tinh"/"Cong an thanh pho"). Quy tac dac biet theo
ghi chu trong van ban:
  - Dong khong danh dau (n) va khong chua "…"/"...." -> ap dung cho ca 34 tinh.
  - Dong co "(1)(2)(4)(5)(6)" -> chi ap dung Ha Noi (G01.801) va TP.HCM (G01.899).
  - Dong co "(3)" -> ap dung Ha Noi, TP.HCM, Hai Phong (G01.839), Da Nang (G01.863).
  - Truong hop dac biet: dong ma .106 KHONG danh dau (ban gop "Ky thuat nghiep
    vu va Ngoai tuyen") chi ap dung cho 32 tinh KHONG phai Ha Noi/TP.HCM (2
    tinh nay dung ban tach rieng .106+.107 co danh dau (1)(2)).
  - Dong co "…" hoac "...." trong ten (ten khong day du, vd "Trai tam giam
    ....", "Benh vien…") -> KHONG tu sinh, in ra danh sach de nguoi dung tu bo
    sung sau (khong du can cu biet tinh nao co).

Cap 3.3: 3377 dong, xa/phuong/don CA thuoc tung Cong an tinh/thanh. Dong trung
ma voi cap 2 la dong "nhac lai don vi cha" - BI LOAI, gan cha = dong tinh gan
nhat truoc do trong bang.

2 ma bi trung do loi van ban goc (khong phai loi parse):
  - G01.602.005 (cap 3.1): "Phong 5" va "Truong Van hoa" (T11) trung ma.
  - G01.909.924 (cap 3.3): "Cong an xa Phu Thuan" va "Don cong an KCN Hoa Phu"
    trung ma.
  => Giu dong xuat hien DAU TIEN trong bang lam ban ghi chinh thuc, dong thu
     hai dua vao danh sach "can xac minh rieng", KHONG tu doan ma thay the.

Mapping cot REF_DON_VI:
    ma          -> MA_DON_VI
    ten (sach)  -> TEN_DON_VI
    ky_hieu     -> TEN_VIET_TAT  (cot "Ghi chu"/"So hieu" trong van ban, vd A01, PA01, CATP Ha Noi)
    parent_ma   -> PARENT_ID (resolve qua map ma -> id moi, 2 pha)

Cac buoc xu ly FK (bang phu thuoc REF_DON_VI, IDM_USERS...) tai su dung dung
logic da kiem chung trong import_don_vi_from_nganh.py.
"""

import argparse
import glob
import os
import re
import sys
import unicodedata

import docx
import oracledb

DB2_DSN = dict(
    user="CAND_QLCNTT",
    password="123456",
    dsn="localhost:1521/XEPDB1",
)

USERS_TABLE = "IDM_USERS"

# Ma cac thanh pho lon co truong hop dac biet trong cap 3.2 (theo ghi chu van ban)
HA_NOI = "G01.801"
TPHCM = "G01.899"
HAI_PHONG = "G01.839"
DA_NANG = "G01.863"

FOOTNOTE_CITY_PREFIX = {
    1: {HA_NOI, TPHCM},
    2: {HA_NOI, TPHCM},
    3: {HA_NOI, TPHCM, HAI_PHONG, DA_NANG},
    4: {HA_NOI, TPHCM},
    5: {HA_NOI, TPHCM},
    6: {HA_NOI, TPHCM},
}


def strip_accents(s: str) -> str:
    return "".join(
        c for c in unicodedata.normalize("NFD", s) if unicodedata.category(c) != "Mn"
    )


def find_input_files(input_dir: str) -> dict[str, str]:
    candidates = glob.glob(os.path.join(input_dir, "*.docx"))
    found: dict[str, str] = {}
    for path in candidates:
        name = strip_accents(os.path.basename(path)).lower()
        if "3.1" in name or "cap31" in name:
            found["cap3_1"] = path
        elif "3.2" in name or "cap32" in name:
            found["cap3_2"] = path
        elif "3.3" in name or "cap33" in name:
            found["cap3_3"] = path
        elif "phu luc" in name or "phu-luc" in name or "cap 2" in name:
            found["cap2"] = path

    missing = {"cap2", "cap3_1", "cap3_2", "cap3_3"} - found.keys()
    if missing:
        raise RuntimeError(
            f"Khong tim du 4 file trong '{input_dir}'. Thieu: {missing}. "
            f"Da tim thay: {list(found.keys())}"
        )
    return found


def read_table_rows(path: str) -> list[list[str]]:
    d = docx.Document(path)
    t = d.tables[0]
    rows = []
    for r in t.rows[1:]:
        rows.append([c.text.strip() for c in r.cells])
    return rows


def parse_cap2(path: str) -> list[dict]:
    rows = read_table_rows(path)
    records = []
    root_ma = None
    for stt, ma, ten, ghichu in (r[:4] for r in rows if len(r) >= 3):
        if not ma:
            continue
        if root_ma is None:
            root_ma = ma  # dong dau = Bo Cong an, la goc
            parent_ma = None
        else:
            parent_ma = root_ma
        records.append(
            {"ma": ma, "ten": ten, "ky_hieu": ghichu, "parent_ma": parent_ma, "source": "cap2"}
        )
    return records


def parse_child_level(path: str, cap2_ma_set: set[str], source: str) -> tuple[list[dict], list[str]]:
    """Dung chung cho cap3_1 va cap3_3: dong nao trung ma voi cap2 la dong
    'nhac lai cha', dong con duoc gan cha = dong cap2 gan nhat truoc do."""
    rows = read_table_rows(path)
    records = []
    dup_log = []
    current_parent = None
    seen_ma: dict[str, int] = {}

    for r in rows:
        if len(r) < 3:
            continue
        _, ma, ten, ghichu = (r + [""] * 4)[:4]
        if not ma:
            continue
        if ma in cap2_ma_set:
            current_parent = ma
            continue  # dong ngu canh, da co trong cap2, khong them lai
        if ma in seen_ma:
            dup_log.append(f"{source}: ma trung '{ma}' - giu ban ghi dau tien, bo qua: {ten!r}")
            continue
        seen_ma[ma] = 1
        records.append(
            {"ma": ma, "ten": ten, "ky_hieu": ghichu, "parent_ma": current_parent, "source": source}
        )
    return records, dup_log


FOOTNOTE_RE = re.compile(r"\s*\((\d)\)\s*$")


def parse_cap3_2_template(path: str) -> tuple[list[dict], list[str]]:
    """Tra ve (danh sach dong mau da phan loai, danh sach dong bi loai can xac nhan tay)."""
    rows = read_table_rows(path)
    template_rows = []
    excluded = []
    for r in rows:
        if len(r) < 3:
            continue
        _, ma, ten, ghichu = (r + [""] * 4)[:4]
        if not ma or "xxx" not in ma:
            continue  # dong context/placeholder dau tien, bo qua

        if "…" in ten or "...." in ten or "..." in ten:
            excluded.append(f"cap3_2 template: '{ma}' - '{ten}' ({ghichu}) - ten khong day du, can bo sung tay")
            continue

        m = FOOTNOTE_RE.search(ten)
        footnote = int(m.group(1)) if m else None
        ten_sach = FOOTNOTE_RE.sub("", ten).strip()

        template_rows.append(
            {"ma_suffix": ma.split(".")[-1], "ten": ten_sach, "ky_hieu": ghichu, "footnote": footnote}
        )
    return template_rows, excluded


def expand_cap3_2(template_rows: list[dict], tinh_list: list[dict]) -> list[dict]:
    """tinh_list: cac dong cap2 la Cong an tinh/thanh, dang {'ma': 'G01.801.000', 'ten': ...}."""
    records = []
    for tinh in tinh_list:
        tinh_prefix = ".".join(tinh["ma"].split(".")[:2])  # vd 'G01.801'
        for tpl in template_rows:
            footnote = tpl["footnote"]
            ma_suffix = tpl["ma_suffix"]

            if footnote is None:
                # Truong hop dac biet: ban gop .106 (khong danh dau) KHONG ap
                # dung cho Ha Noi/TP.HCM - 2 tinh nay dung ban tach (1)(2).
                if ma_suffix == "106" and tinh_prefix in FOOTNOTE_CITY_PREFIX[1]:
                    continue
                applies = True
            else:
                applies = tinh_prefix in FOOTNOTE_CITY_PREFIX.get(footnote, set())

            if not applies:
                continue

            new_ma = f"{tinh_prefix}.{ma_suffix}"
            records.append(
                {
                    "ma": new_ma,
                    "ten": tpl["ten"],
                    "ky_hieu": tpl["ky_hieu"],
                    "parent_ma": tinh["ma"],
                    "source": "cap3_2_expanded",
                }
            )
    return records


def build_all_records(files: dict[str, str]) -> tuple[list[dict], list[str], list[str]]:
    cap2_records = parse_cap2(files["cap2"])
    cap2_ma_set = {r["ma"] for r in cap2_records}

    cap3_1_records, dup_log_1 = parse_child_level(files["cap3_1"], cap2_ma_set, "cap3_1")
    cap3_3_records, dup_log_3 = parse_child_level(files["cap3_3"], cap2_ma_set, "cap3_3")

    tinh_list = [
        r for r in cap2_records
        if r["ten"].startswith("Công an tỉnh") or r["ten"].startswith("Công an thành phố")
        or r["ten"].startswith("Công an Thành phố")
    ]
    template_rows, excluded = parse_cap3_2_template(files["cap3_2"])
    cap3_2_records = expand_cap3_2(template_rows, tinh_list)

    print(f"  Cap 2: {len(cap2_records)} dong (trong do {len(tinh_list)} tinh/thanh)")
    print(f"  Cap 3.1 (da loc dong ngu canh): {len(cap3_1_records)} dong")
    print(f"  Cap 3.3 (da loc dong ngu canh): {len(cap3_3_records)} dong")
    print(f"  Cap 3.2 (sinh tu template cho {len(tinh_list)} tinh): {len(cap3_2_records)} dong")

    all_records = cap2_records + cap3_1_records + cap3_3_records + cap3_2_records
    dup_log = dup_log_1 + dup_log_3
    return all_records, dup_log, excluded


# ---------------------------------------------------------------------------
# Phan ghi vao DB2 (giong logic da kiem chung trong import_don_vi_from_nganh.py)
# ---------------------------------------------------------------------------


def find_all_fk_edges(cur) -> list[tuple[str, str]]:
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
    cur = conn2.cursor()
    dependents = transitive_dependents(edges, "REF_DON_VI")
    pending = sorted(t for t in dependents if t != USERS_TABLE)
    print(f"  Bang se xoa (truc tiep + gian tiep): {pending}")

    max_rounds = len(pending) + 2
    for _ in range(max_rounds):
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
    cur.execute("UPDATE REF_DON_VI SET PARENT_ID = NULL")
    cur.execute("DELETE FROM REF_DON_VI")
    conn2.commit()
    cur.close()


def import_pass1_insert_flat(conn2, records: list[dict]) -> dict[str, int]:
    cur = conn2.cursor()
    id_map: dict[str, int] = {}

    for rec in records:
        new_id_var = cur.var(int)
        cur.execute(
            """
            INSERT INTO REF_DON_VI
                (MA_DON_VI, TEN_DON_VI, TEN_VIET_TAT, IS_ACTIVE, PARENT_ID, CREATED_AT)
            VALUES
                (:ma_don_vi, :ten_don_vi, :ten_viet_tat, 1, NULL, SYSTIMESTAMP)
            RETURNING ID INTO :new_id
            """,
            {
                "ma_don_vi": rec["ma"],
                "ten_don_vi": rec["ten"] or rec["ma"],
                "ten_viet_tat": rec["ky_hieu"] or None,
                "new_id": new_id_var,
            },
        )
        id_map[rec["ma"]] = int(new_id_var.getvalue()[0])

    conn2.commit()
    cur.close()
    return id_map


def import_pass2_set_parent(conn2, records: list[dict], id_map: dict[str, int]) -> list[str]:
    cur = conn2.cursor()
    orphans: list[str] = []

    for rec in records:
        parent_ma = rec["parent_ma"]
        if not parent_ma:
            continue
        child_local_id = id_map.get(rec["ma"])
        parent_local_id = id_map.get(parent_ma)
        if parent_local_id is None:
            orphans.append(rec["ma"])
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
    cur.execute(f"UPDATE {USERS_TABLE} SET DON_VI_ID = :root_id", {"root_id": root_id})
    n = cur.rowcount
    conn2.commit()
    cur.close()
    return n


def validate(conn2, records: list[dict], id_map: dict[str, int], orphans: list[str]):
    cur = conn2.cursor()
    cur.execute("SELECT COUNT(*) FROM REF_DON_VI")
    total_imported = cur.fetchone()[0]
    cur.execute(
        "SELECT MA_DON_VI, COUNT(*) FROM REF_DON_VI GROUP BY MA_DON_VI HAVING COUNT(*) > 1"
    )
    dup_ma_don_vi = cur.fetchall()
    cur.close()

    print("\n=== VALIDATE ===")
    print(f"So ban ghi du kien import: {len(records)}")
    print(f"So ban ghi thuc te trong REF_DON_VI: {total_imported}")
    print(f"MA_DON_VI trung lap: {dup_ma_don_vi or 'khong co'}")
    print(f"Parent mo coi (khong tim thay cha): {orphans or 'khong co'}")

    ok = total_imported == len(records) and not dup_ma_don_vi and not orphans
    print("=> " + ("OK" if ok else "CO VAN DE, kiem tra lai truoc khi dung du lieu"))
    return ok


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--input-dir", default=os.path.dirname(os.path.abspath(__file__)))
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    print(f"Doc 4 file docx trong: {args.input_dir}")
    files = find_input_files(args.input_dir)
    for k, v in files.items():
        print(f"  {k}: {os.path.basename(v)}")

    print("\nParse & xu ly du lieu...")
    records, dup_log, excluded = build_all_records(files)

    print(f"\nTong so ban ghi se import: {len(records)}")
    if dup_log:
        print("\nCAC MA TRUNG DO LOI VAN BAN GOC (da giu ban dau tien, bo qua ban sau):")
        for line in dup_log:
            print("  -", line)
    if excluded:
        print("\nCAC DONG TRONG CAP 3.2 KHONG TU SINH DUOC (can bo sung tay):")
        for line in excluded:
            print("  -", line)

    if args.dry_run:
        print("\n--dry-run: khong ghi gi vao DB2. 5 ban ghi mau:")
        for r in records[:5]:
            print(" ", r)
        return

    print("\nKet noi DB2 (REF_DON_VI) ...")
    conn2 = oracledb.connect(**DB2_DSN)
    try:
        cur2 = conn2.cursor()
        edges = find_all_fk_edges(cur2)
        cur2.close()

        print("\nXoa du lieu demo o cac bang phu thuoc (tru IDM_USERS)...")
        wipe_dependent_tables(conn2, edges)

        fk_name = disable_users_fk(conn2)
        id_map: dict[str, int] = {}
        orphans: list[str] = []
        try:
            print("\nDon REF_DON_VI hien tai...")
            wipe_ref_don_vi(conn2)

            print("Pha 1: insert phang...")
            id_map = import_pass1_insert_flat(conn2, records)

            print("Pha 2: gan PARENT_ID...")
            orphans = import_pass2_set_parent(conn2, records, id_map)

            root_ma = next((r["ma"] for r in records if r["parent_ma"] is None), None)
            root_id = id_map.get(root_ma) if root_ma else None
            if root_id is not None:
                n = remap_users_to_root(conn2, root_id)
                print(f"\nDa gan lai {n} tai khoan (IDM_USERS) ve don vi goc ({root_ma}, ID={root_id}).")
                print("=> Vao UI gan lai DUNG don vi cho tung user demo neu can (tru admin o goc).")
            else:
                print("\nCANH BAO: khong tim thay ID moi cua don vi goc, chua remap IDM_USERS.DON_VI_ID.")
        finally:
            enable_users_fk(conn2, fk_name)

        ok = validate(conn2, records, id_map, orphans)
        sys.exit(0 if ok else 1)
    finally:
        conn2.close()


if __name__ == "__main__":
    main()
