# 扫描 EBWebView/Default 目录的全部内容
import os

base_dir = os.path.expanduser("~\\AppData\\Local\\CustomizedBrowser\\Instances")
instances = os.listdir(base_dir)

for inst in instances:
    default_dir = os.path.join(base_dir, inst, "EBWebView", "Default")
    if not os.path.isdir(default_dir):
        continue
    
    print(f"实例: {inst} / EBWebView / Default")
    print("=" * 80)
    
    entries = sorted(os.listdir(default_dir))
    print(f"共 {len(entries)} 个条目:\n")
    
    for e in entries:
        p = os.path.join(default_dir, e)
        if os.path.isdir(p):
            fc = sum(len(files) for _, _, files in os.walk(p))
            sz = sum(os.path.getsize(os.path.join(dp, f)) for dp, dn, fn in os.walk(p) for f in fn)
            print(f"[DIR]  {e:50s}  文件: {fc:4d}  大小: {sz/1024:.1f} KB")
        else:
            sz = os.path.getsize(p)
            print(f"[FILE] {e:50s}  大小: {sz/1024:.1f} KB")
    
    # 只扫一个实例
    break