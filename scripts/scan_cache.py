# 扫描实例缓存目录，查看实际文件结构
import os
import sys

# 查找实例数据目录
base_dir = os.path.join(os.path.dirname(__file__), "..", "bin", "Debug", "net8.0-windows", "Instances")
if not os.path.exists(base_dir):
    base_dir = os.path.expanduser("~\\AppData\\Local\\CustomizedBrowser\\Instances")

print(f"扫描实例目录: {base_dir}")
print("=" * 80)

if not os.path.exists(base_dir):
    print("实例目录不存在！")
    sys.exit(1)

# 列出所有实例
instances = os.listdir(base_dir)
print(f"找到 {len(instances)} 个实例:")
for inst in instances:
    inst_path = os.path.join(base_dir, inst)
    print(f"\n{'='*80}")
    print(f"实例: {inst}")
    print(f"{'='*80}")
    
    # 列出实例目录下的所有条目
    if os.path.isdir(inst_path):
        entries = os.listdir(inst_path)
        print(f"  目录条目 ({len(entries)} 个):")
        for entry in sorted(entries):
            entry_path = os.path.join(inst_path, entry)
            if os.path.isdir(entry_path):
                size = sum(os.path.getsize(os.path.join(dp, f)) for dp, dn, fn in os.walk(entry_path) for f in fn) if os.path.exists(entry_path) else 0
                file_count = sum(len(files) for _, _, files in os.walk(entry_path))
                print(f"    [目录] {entry:30s} 大小: {size/1024:.1f} KB, 文件数: {file_count}")
            else:
                size = os.path.getsize(entry_path)
                print(f"    [文件] {entry:30s} 大小: {size/1024:.1f} KB")
        
        # 详细扫描域名相关目录
        scan_dirs = ["Local Storage", "Session Storage", "IndexedDB", "CacheStorage", "Service Worker", "Cache", "Code Cache", "GPUCache"]
        for scan_dir in scan_dirs:
            scan_path = os.path.join(inst_path, scan_dir)
            if os.path.isdir(scan_path):
                print(f"\n  --- {scan_dir} 详细内容 ---")
                entries = sorted(os.listdir(scan_path))
                if not entries:
                    print(f"    (空目录)")
                else:
                    for entry in entries[:30]:  # 最多显示30个
                        entry_path = os.path.join(scan_path, entry)
                        if os.path.isdir(entry_path):
                            fc = sum(len(files) for _, _, files in os.walk(entry_path))
                            sz = sum(os.path.getsize(os.path.join(dp, f)) for dp, dn, fn in os.walk(entry_path) for f in fn)
                            print(f"    [目录] {entry:50s} 文件数:{fc:4d} 大小:{sz/1024:.1f}KB")
                        else:
                            sz = os.path.getsize(entry_path)
                            print(f"    [文件] {entry:50s} 大小:{sz/1024:.1f}KB")
                    if len(entries) > 30:
                        print(f"    ... 还有 {len(entries)-30} 个条目")