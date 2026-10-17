# 扫描关键的缓存数据目录内容
import os
import json
import sqlite3
import tempfile
import shutil

base_dir = os.path.expanduser("~\\AppData\\Local\\CustomizedBrowser\\Instances")

for inst in os.listdir(base_dir):
    default_dir = os.path.join(base_dir, inst, "EBWebView", "Default")
    cache_dir = os.path.join(base_dir, inst, "Cache", "Default")
    if not os.path.isdir(default_dir):
        continue
    
    print(f"实例: {inst}")
    print("=" * 80)
    
    # 1. Local Storage 详细内容
    ls_dir = os.path.join(default_dir, "Local Storage")
    if os.path.isdir(ls_dir):
        print(f"\n=== Local Storage ({os.path.getsize(ls_dir)} bytes) ===")
        for e in sorted(os.listdir(ls_dir)):
            p = os.path.join(ls_dir, e)
            if os.path.isdir(p):
                # leveldb 子目录
                for f in sorted(os.listdir(p)):
                    fp = os.path.join(p, f)
                    print(f"  {e}/{f}  ({os.path.getsize(fp)} bytes)")
            else:
                # 尝试解释文件名
                name = os.path.splitext(e)[0]
                try:
                    # leveldb 格式
                    pass
                except:
                    pass
                print(f"  {e}  ({os.path.getsize(p)} bytes)")
    
    # 2. Session Storage 详细内容
    ss_dir = os.path.join(default_dir, "Session Storage")
    if os.path.isdir(ss_dir):
        print(f"\n=== Session Storage ===")
        for e in sorted(os.listdir(ss_dir)):
            p = os.path.join(ss_dir, e)
            print(f"  {e}  ({os.path.getsize(p)} bytes)" if os.path.isfile(p) else f"  [DIR] {e}")
    
    # 3. Network 目录
    net_dir = os.path.join(default_dir, "Network")
    if os.path.isdir(net_dir):
        print(f"\n=== Network ===")
        for e in sorted(os.listdir(net_dir)):
            p = os.path.join(net_dir, e)
            sz = os.path.getsize(p) if os.path.isfile(p) else 0
            print(f"  {e}  ({sz} bytes)")
    
    # 4. WebStorage 目录
    ws_dir = os.path.join(default_dir, "WebStorage")
    if os.path.isdir(ws_dir):
        print(f"\n=== WebStorage ===")
        for e in sorted(os.listdir(ws_dir)):
            p = os.path.join(ws_dir, e)
            if os.path.isdir(p):
                for f in sorted(os.listdir(p)):
                    fp = os.path.join(p, f)
                    print(f"  {e}/{f}  ({os.path.getsize(fp)} bytes)" if os.path.isfile(fp) else f"  [DIR] {e}/{f}")
            else:
                print(f"  {e}  ({os.path.getsize(p)} bytes)")
    
    # 5. 查看 Cache 目录
    if os.path.isdir(cache_dir):
        print(f"\n=== Cache/Default (总大小: {sum(os.path.getsize(os.path.join(dp, f)) for dp, dn, fn in os.walk(cache_dir) for f in fn)} bytes) ===")
        entries = sorted(os.listdir(cache_dir))
        for e in entries[:20]:
            p = os.path.join(cache_dir, e)
            if os.path.isdir(p):
                fc = sum(len(files) for _, _, files in os.walk(p))
                print(f"  [DIR] {e}  ({fc} files)")
            else:
                print(f"  {e}  ({os.path.getsize(p)} bytes)")
        if len(entries) > 20:
            print(f"  ... 还有 {len(entries)-20} 个条目")
    
    # 6. 查看是否有 Cookies 等 SQLite 数据库中的域名
    # 尝试读取 SQLite 数据库中的网站信息
    for db_name in ["Cookies", "History", "Web Data", "Login Data", "Top Sites", "Favicons"]:
        db_path = os.path.join(default_dir, db_name)
        if os.path.isfile(db_path) and os.path.getsize(db_path) > 0:
            try:
                # 复制到临时文件再读取，避免锁定
                tmp = tempfile.NamedTemporaryFile(delete=False, suffix='.db')
                tmp.close()
                shutil.copy2(db_path, tmp.name)
                
                conn = sqlite3.connect(tmp.name)
                cursor = conn.cursor()
                
                # 获取表名
                cursor.execute("SELECT name FROM sqlite_master WHERE type='table'")
                tables = [row[0] for row in cursor.fetchall()]
                
                # 尝试从各表中提取域名
                print(f"\n=== {db_name} ({os.path.getsize(db_path)} bytes) - 表: {tables} ===")
                
                for table in tables:
                    try:
                        # 查找包含 host/domain/url/site 的列
                        cursor.execute(f"PRAGMA table_info(\"{table}\")")
                        cols = [row[1] for row in cursor.fetchall()]
                        
                        url_cols = [c for c in cols if any(k in c.lower() for k in ['host', 'domain', 'url', 'site', 'origin'])]
                        if url_cols:
                            col_names = ', '.join(url_cols[:3])
                            cursor.execute(f"SELECT DISTINCT {col_names} FROM \"{table}\" LIMIT 10")
                            rows = cursor.fetchall()
                            if rows:
                                for row in rows[:5]:
                                    vals = [str(v)[:60] for v in row]
                                    print(f"  [{table}] {' | '.join(vals)}")
                                if len(rows) > 5:
                                    print(f"  ... 还有 {len(rows)-5} 行")
                    except:
                        pass
                
                conn.close()
                os.unlink(tmp.name)
            except Exception as ex:
                print(f"\n=== {db_name} - 无法读取: {ex}")
    
    # 只扫一个实例
    break