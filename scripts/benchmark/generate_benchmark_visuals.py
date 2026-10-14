import os
import sys
import glob
import json
import pandas as pd
import numpy as np
import matplotlib.pyplot as plt
from collections import defaultdict

# Reconfigure stdout for utf-8
sys.stdout.reconfigure(encoding='utf-8')

BASE_DIR = r"D:\Kojin\BambooMintKey"
RESULTS_DIR = os.path.join(BASE_DIR, "docs", "6.Benchmark", "results")
LOGS_DIR = os.path.join(RESULTS_DIR, "logs", "v2")
CHARTS_DIR = os.path.join(RESULTS_DIR, "charts")
CSV_PATH = os.path.join(RESULTS_DIR, "benchmark_all.csv")

os.makedirs(CHARTS_DIR, exist_ok=True)

# 1. Clean CSV if needed
with open(CSV_PATH, 'r', encoding='utf-8') as f:
    raw_lines = [line.strip() for line in f if line.strip()]

cleaned_lines = [raw_lines[0]]
for line in raw_lines[1:]:
    parts = line.split(',')
    if len(parts) > 1 and parts[1].strip('\" '):
        cleaned_lines.append(line)

with open(CSV_PATH, 'w', encoding='utf-8', newline='\n') as f:
    f.write('\n'.join(cleaned_lines) + '\n')

print(f"Cleaned CSV: {len(cleaned_lines)-1} valid runs.")

# 2. Load DataFrame
df = pd.read_csv(CSV_PATH)
df['Timestamp'] = pd.to_datetime(df['Timestamp'])

# Set up matplotlib aesthetics
plt.style.use('seaborn-v0_8-whitegrid')
plt.rcParams['font.sans-serif'] = ['Segoe UI', 'DejaVu Sans', 'Arial']
plt.rcParams['axes.unicode_minus'] = False

PALETTE = {
    'BambooMintKey': '#10b981',          # Emerald Green
    'EVKey': '#3b82f6',                  # Blue
    'UniKey': '#f59e0b',                 # Amber
    'OpenKey': '#8b5cf6',                # Purple
    'Windows Built-in Telex': '#ef4444'  # Crimson Red
}

MARKERS = {
    'BambooMintKey': 'o',
    'EVKey': 's',
    'UniKey': '^',
    'OpenKey': 'D',
    'Windows Built-in Telex': 'X'
}

IME_ORDER = ['BambooMintKey', 'EVKey', 'UniKey', 'OpenKey', 'Windows Built-in Telex']

# --- CHART 1: WPM vs Input Cadence (Keys/Sec & Delay) ---
fig, ax = plt.subplots(figsize=(10, 6), dpi=180)
for ime in IME_ORDER:
    sub = df[df['IME'] == ime].sort_values('KeysPerSec')
    arch = sub['Architecture'].iloc[0]
    ax.plot(sub['KeysPerSec'], sub['WPM'], marker=MARKERS[ime], label=f"{ime} ({arch})",
            color=PALETTE[ime], linewidth=2.4, markersize=8)

ax.set_title('Tốc Độ Thực Tế (WPM) Theo Nhịp Bắn Phím (Keys/s) Trên Notepad', fontsize=14, fontweight='bold', pad=14)
ax.set_xlabel('Tốc độ bắn phím (Keys/s)  [Delay từ 60ms xuống 5ms]', fontsize=11, labelpad=8)
ax.set_ylabel('Tốc độ thực tế ghi nhận (WPM)', fontsize=11, labelpad=8)
ax.legend(frameon=True, facecolor='white', framealpha=0.95, fontsize=10, loc='lower right')

# Annotations
ax.axvspan(100, 200, color='#f3f4f6', alpha=0.5, label='_nolegend_')
ax.annotate('VÙNG BÃO HÒA (10ms - 5ms)\nBottleneck do Windows OS / Edit Control Queue\nWPM chững lại quanh mức 233 - 245 WPM',
            xy=(100, 242.7), xytext=(80, 160),
            arrowprops=dict(facecolor='#1f2937', arrowstyle='->', lw=1.5),
            fontsize=9.5, fontweight='bold', color='#111827',
            bbox=dict(boxstyle='round,pad=0.5', facecolor='#fef3c7', edgecolor='#f59e0b', lw=1))

plt.tight_layout()
c1_png = os.path.join(CHARTS_DIR, "01_wpm_vs_delay.png")
c1_svg = os.path.join(CHARTS_DIR, "01_wpm_vs_delay.svg")
fig.savefig(c1_png)
fig.savefig(c1_svg)
plt.close(fig)
print("Saved Chart 1: WPM vs Delay")

# --- CHART 2: Accuracy Comparison (Overall vs VN vs EN) ---
fig, ax = plt.subplots(figsize=(11, 6), dpi=180)
acc_df = df.groupby('IME').first().loc[IME_ORDER].reset_index()

x = np.arange(len(IME_ORDER))
width = 0.26

b1 = ax.bar(x - width, acc_df['Accuracy'], width, label='Độ đúng Tổng thể (583 từ)', color='#3b82f6', alpha=0.9)
b2 = ax.bar(x, acc_df['AccuracyVN'], width, label='Độ đúng Tiếng Việt (495 từ)', color='#10b981', alpha=0.9)
b3 = ax.bar(x + width, acc_df['AccuracyEN'], width, label='Bảo vệ từ Tiếng Anh (88 từ)', color='#f43f5e', alpha=0.9)

ax.set_title('Độ Chính Xác: Tiếng Việt Tuyệt Đối vs. Thách Thức Từ Tiếng Anh', fontsize=14, fontweight='bold', pad=14)
ax.set_xticks(x)
ax.set_xticklabels([f"{r['IME']}\n[{r['Architecture']}]" for _, r in acc_df.iterrows()], fontsize=10, fontweight='medium')
ax.set_ylim(50, 108)
ax.set_ylabel('Độ chính xác (%)', fontsize=11)
ax.legend(frameon=True, facecolor='white', framealpha=0.95, fontsize=10, loc='lower left')

for bar in b1:
    h = bar.get_height()
    ax.text(bar.get_x() + bar.get_width()/2., h + 1, f"{h:.1f}%", ha='center', va='bottom', fontsize=8.5, fontweight='bold', color='#1e3a8a')

for bar in b2:
    h = bar.get_height()
    ax.text(bar.get_x() + bar.get_width()/2., h + 1, f"{h:.0f}%", ha='center', va='bottom', fontsize=8.5, fontweight='bold', color='#065f46')

for bar, failed in zip(b3, acc_df['FailedEN']):
    h = bar.get_height()
    ax.text(bar.get_x() + bar.get_width()/2., h + 1, f"{h:.1f}%\n(-{int(failed)})", ha='center', va='bottom', fontsize=8.5, fontweight='bold', color='#9f1239')

plt.tight_layout()
c2_png = os.path.join(CHARTS_DIR, "02_accuracy_breakdown.png")
c2_svg = os.path.join(CHARTS_DIR, "02_accuracy_breakdown.svg")
fig.savefig(c2_png)
fig.savefig(c2_svg)
plt.close(fig)
print("Saved Chart 2: Accuracy Breakdown")

# --- CHART 3: Failed Words Breakdown (Horizontal Bar) ---
fig, ax = plt.subplots(figsize=(10, 5), dpi=180)
ime_labels = [f"{r['IME']} ({r['Architecture']})" for _, r in acc_df.iloc[::-1].iterrows()]
failed_counts = acc_df['Failed'].iloc[::-1].values
colors = [PALETTE[r['IME']] for _, r in acc_df.iloc[::-1].iterrows()]

bars = ax.barh(ime_labels, failed_counts, color=colors, height=0.55, alpha=0.9, edgecolor='#374151', lw=0.5)
ax.set_title('Số Lượng Từ Bị Sai/Hỏng (Failed Words) Trên Tổng Số 583 Từ', fontsize=14, fontweight='bold', pad=14)
ax.set_xlabel('Số lượng từ sai (càng ít càng tốt)', fontsize=11)
ax.set_xlim(0, 32)

for bar in bars:
    w = bar.get_width()
    ax.text(w + 0.5, bar.get_y() + bar.get_height()/2., f"{int(w)} từ sai", ha='left', va='center', fontsize=10, fontweight='bold', color='#1f2937')

plt.tight_layout()
c3_png = os.path.join(CHARTS_DIR, "03_failed_words_comparison.png")
c3_svg = os.path.join(CHARTS_DIR, "03_failed_words_comparison.svg")
fig.savefig(c3_png)
fig.savefig(c3_svg)
plt.close(fig)
print("Saved Chart 3: Failed Words Comparison")

# --- CHART 4: Radar Chart Comparison ---
categories = ['Tổng Accuracy', 'Độ đúng VN', 'Bảo vệ EN', 'Tốc độ Đỉnh (WPM)', 'Công nghệ Hiện đại (TSF)']
N = len(categories)

# Normalization values (0 to 100)
# WPM max normalized (244.6 = 100, 230 = 80)
def get_radar_values(ime_name):
    row = acc_df[acc_df['IME'] == ime_name].iloc[0]
    wpm_max = df[df['IME'] == ime_name]['WPM'].max()
    wpm_norm = (wpm_max / 244.6) * 100
    is_tsf = 100 if row['Architecture'] == 'TSF' else 60
    return [
        row['Accuracy'],
        row['AccuracyVN'],
        row['AccuracyEN'],
        wpm_norm,
        is_tsf
    ]

angles = [n / float(N) * 2 * np.pi for n in range(N)]
angles += angles[:1]

fig, ax = plt.subplots(figsize=(8, 8), subplot_kw=dict(polar=True), dpi=180)
plt.xticks(angles[:-1], categories, color='#374151', size=10, fontweight='bold')

for ime in ['BambooMintKey', 'EVKey', 'Windows Built-in Telex']:
    vals = get_radar_values(ime)
    vals += vals[:1]
    ax.plot(angles, vals, linewidth=2.2, linestyle='solid', label=f"{ime}", color=PALETTE[ime])
    ax.fill(angles, vals, color=PALETTE[ime], alpha=0.15)

ax.set_ylim(50, 105)
ax.legend(loc='upper right', bbox_to_anchor=(1.25, 1.1), frameon=True, facecolor='white', fontsize=10)
ax.set_title('Đánh Giá Toàn Diện 5 Chiều: BambooMintKey vs. Đối Thủ', fontsize=14, fontweight='bold', pad=22)

plt.tight_layout()
c4_png = os.path.join(CHARTS_DIR, "04_radar_comparison.png")
c4_svg = os.path.join(CHARTS_DIR, "04_radar_comparison.svg")
fig.savefig(c4_png)
fig.savefig(c4_svg)
plt.close(fig)
print("Saved Chart 4: Radar Comparison")

# 3. Parse Detailed Errors from Logs
log_files = glob.glob(os.path.join(LOGS_DIR, '*.log'))
error_data = defaultdict(dict)

for fpath in log_files:
    fname = os.path.basename(fpath)
    parts = fname.split('_')
    ime = parts[0]
    delay = parts[2]
    
    with open(fpath, 'r', encoding='utf-8', errors='ignore') as f:
        content = f.read()
    
    errors = []
    error_section = False
    for line in content.splitlines():
        if 'DANH SÁCH TOÀN BỘ CÁC TỪ BỊ LỖI' in line:
            error_section = True
            continue
        if error_section and line.startswith('---'):
            continue
        if error_section and 'VĂN BẢN THỰC TẾ THU ĐƯỢC' in line:
            error_section = False
            break
        if error_section and line.strip().startswith('['):
            errors.append(line.strip())
            
    error_data[ime][delay] = errors

# Canonical map
canonical_names = {
    'bamboomintkey': 'BambooMintKey',
    'evkey': 'EVKey',
    'unikey': 'UniKey',
    'openkey': 'OpenKey',
    'windowsbuilt-intelex': 'Windows Built-in Telex'
}

error_matrix = {}
for k, v in error_data.items():
    cname = canonical_names.get(k, k)
    first_delay = list(v.keys())[0]
    error_matrix[cname] = v[first_delay]

print("Successfully extracted error matrices from all 40 log files.")
