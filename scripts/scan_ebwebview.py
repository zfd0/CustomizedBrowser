# 扫描 EBWebView 目录结构，找出实际缓存数据存放位置
import os
import sys

base_dir = os.path.expanduser("~\\AppData\\Local\\CustomizedBrowser\\Instances")
print(f"扫描实例目录: {base_dir}")
print("=" * 80)

if not os.path.exists(base_dir):
    print("实例目录不存在！")
    sys.exit(1)

instances = os.listdir(base_dir)
for inst in instances:
    ebwebview = os.path.join(base_dir, inst, "EBWebView")
    if not os.path.isdir(ebwebview):
        continue
    
    print(f"\n{'='*80}")
    print(f"实例: {inst} / EBWebView")
    print(f"{'='*80}")
    
    # 第一层
    entries = sorted(os.listdir(ebwebview))
    print(f"\n第一层 ({len(entries)} 个条目):")
    for e in entries:
        p = os.path.join(ebwebview, e)
        if os.path.isdir(p):
            fc = sum(len(files) for _, _, files in os.walk(p))
            sz = sum(os.path.getsize(os.path.join(dp, f)) for dp, dn, fn in os.walk(p) for f in fn)
            print(f"  [DIR] {e:45s} 文件:{fc:4d}  大小:{sz/1024:.1f}KB")
        else:
            sz = os.path.getsize(p)
            print(f"  [FILE] {e:45s} 大小:{sz/1024:.1f}KB")
    
    # 扫描关键子目录
    key_dirs = ["Default", "Guest Profile", "System Profile", "BrowserMetrics", 
                "CacheStorage", "Local Storage", "Session Storage", "IndexedDB",
                "Service Worker", "File System", "blob_storage", "databases",
                "Download Service", "Extension Rules", "Extension State",
                "Extension Scripts", "GCM Store", "Network Action Predictor",
                "Network", "Pepper Data", "Platform Notifications",
                "Recovery", "Sessions", "Sync Data", "shared_proto_db",
                "VideoDecodeStats", "Web Applications", "webrtc_event_logs"]
    
    for key_dir in key_dirs:
        kd_path = os.path.join(ebwebview, key_dir)
        if os.path.isdir(kd_path):
            # 获取子目录的内容
            entries = sorted(os.listdir(kd_path)) if os.path.isdir(kd_path) else []
            print(f"\n--- {key_dir} ---")
            if not entries:
                print(f"  (空目录)")
            else:
                # 显示前20个条目
                for e in entries[:20]:
                    p = os.path.join(kd_path, e)
                    if os.path.isdir(p):
                        fc = sum(len(files) for _, _, files in os.walk(p))
                        sz = sum(os.path.getsize(os.path.join(dp, f)) for dp, dn, fn in os.walk(p) for f in fn)
                        print(f"  [DIR] {e:50s} 文件:{fc:4d} 大小:{sz/1024:.1f}KB")
                    else:
                        sz = os.path.getsize(p)
                        print(f"  [FILE] {e:50s} 大小:{sz/1024:.1f}KB")
                if len(entries) > 20:
                    print(f"  ... 还有 {len(entries)-20} 个条目")
    
    # 只扫描一个实例就够了
    break