"""
텍스처 색상 변환 도구 (Hue Shift)
- 파란색 계열 텍스처를 다른 색상으로 변환합니다.
- PIL (Pillow) 라이브러리 필요: pip install Pillow

사용법:
    python texture_recolor.py
    
    또는 특정 파일만:
    python texture_recolor.py --input "path/to/texture.png" --target_hue 0 --output "path/to/output.png"
"""

import os
import sys
import colorsys
from PIL import Image
import numpy as np

# ============================================================
# 색상 정의 (Hue 값: 0~360도)
# ============================================================
COLOR_VARIANTS = {
    "Yellow": 60,     # 노랑
    "Orange": 30,     # 주황
    "Green":  120,    # 초록
    "Purple": 270,    # 보라
    "Pink":   330,    # 분홍
}

# 원본 파란색의 Hue 범위 (대략 200~240도)
SOURCE_HUE_CENTER = 220  # 파란색 중심 Hue

# ============================================================
# 변환할 텍스처 목록
# ============================================================
TEXTURE_DIR = os.path.join(os.path.dirname(__file__), 
    "Assets", "Eric VFX Studio", "Resource", "Textures")

TEXTURES_TO_CONVERT = [
    "Electro_03_Blue.png",
    "Electro03.png",
]

# ============================================================
# 핵심 변환 함수
# ============================================================
def shift_hue(image_path, target_hue_deg, output_path):
    """
    이미지의 Hue를 target_hue_deg로 시프트합니다.
    파란색 계열(Hue ~200-240)만 대상으로 변환합니다.
    
    Args:
        image_path: 원본 이미지 경로
        target_hue_deg: 목표 Hue (0-360도)
        output_path: 출력 이미지 경로
    """
    img = Image.open(image_path).convert("RGBA")
    pixels = np.array(img, dtype=np.float32)
    
    # RGBA 분리
    r = pixels[:, :, 0] / 255.0
    g = pixels[:, :, 1] / 255.0
    b = pixels[:, :, 2] / 255.0
    a = pixels[:, :, 3]  # 알파는 그대로 유지
    
    # RGB -> HSV 변환 (벡터화)
    h = np.zeros_like(r)
    s = np.zeros_like(r)
    v = np.zeros_like(r)
    
    rows, cols = r.shape
    for y in range(rows):
        for x in range(cols):
            hh, ss, vv = colorsys.rgb_to_hsv(r[y, x], g[y, x], b[y, x])
            h[y, x] = hh  # 0~1 범위
            s[y, x] = ss
            v[y, x] = vv
    
    # Hue 시프트 계산
    source_hue_norm = SOURCE_HUE_CENTER / 360.0  # 파란색 Hue (정규화)
    target_hue_norm = target_hue_deg / 360.0
    hue_delta = target_hue_norm - source_hue_norm
    
    # 파란색 계열 픽셀만 선택 (Hue: 180~260도 범위, 채도 > 0.05)
    blue_hue_min = 180.0 / 360.0
    blue_hue_max = 260.0 / 360.0
    
    mask = (h >= blue_hue_min) & (h <= blue_hue_max) & (s > 0.05)
    
    # Hue 시프트 적용
    h[mask] = (h[mask] + hue_delta) % 1.0
    
    # HSV -> RGB 변환
    r_out = np.zeros_like(r)
    g_out = np.zeros_like(g)
    b_out = np.zeros_like(b)
    
    for y in range(rows):
        for x in range(cols):
            rr, gg, bb = colorsys.hsv_to_rgb(h[y, x], s[y, x], v[y, x])
            r_out[y, x] = rr
            g_out[y, x] = gg
            b_out[y, x] = bb
    
    # 결과 조합
    result = np.stack([
        (r_out * 255).astype(np.uint8),
        (g_out * 255).astype(np.uint8),
        (b_out * 255).astype(np.uint8),
        a.astype(np.uint8)
    ], axis=2)
    
    result_img = Image.fromarray(result, "RGBA")
    
    # 출력 디렉토리 생성
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    result_img.save(output_path)
    print(f"  [OK] Saved: {output_path}")


def shift_hue_fast(image_path, target_hue_deg, output_path):
    """
    numpy 벡터화 버전 - 큰 텍스처에서 훨씬 빠릅니다.
    """
    img = Image.open(image_path).convert("RGBA")
    data = np.array(img, dtype=np.float64)
    
    r = data[:, :, 0] / 255.0
    g = data[:, :, 1] / 255.0
    b = data[:, :, 2] / 255.0
    a = data[:, :, 3]
    
    # Vectorized RGB to HSV
    cmax = np.maximum(np.maximum(r, g), b)
    cmin = np.minimum(np.minimum(r, g), b)
    delta = cmax - cmin
    
    # Hue
    h = np.zeros_like(r)
    mask_r = (cmax == r) & (delta > 0)
    mask_g = (cmax == g) & (delta > 0)
    mask_b = (cmax == b) & (delta > 0)
    
    h[mask_r] = (((g[mask_r] - b[mask_r]) / delta[mask_r]) % 6) / 6.0
    h[mask_g] = (((b[mask_g] - r[mask_g]) / delta[mask_g]) + 2) / 6.0
    h[mask_b] = (((r[mask_b] - g[mask_b]) / delta[mask_b]) + 4) / 6.0
    
    # Saturation
    s = np.zeros_like(r)
    nonzero = cmax > 0
    s[nonzero] = delta[nonzero] / cmax[nonzero]
    
    # Value
    v = cmax
    
    # Hue shift
    source_hue_norm = SOURCE_HUE_CENTER / 360.0
    target_hue_norm = target_hue_deg / 360.0
    hue_delta = target_hue_norm - source_hue_norm
    
    blue_hue_min = 180.0 / 360.0
    blue_hue_max = 260.0 / 360.0
    blue_mask = (h >= blue_hue_min) & (h <= blue_hue_max) & (s > 0.05)
    
    h[blue_mask] = (h[blue_mask] + hue_delta) % 1.0
    
    # Vectorized HSV to RGB
    h6 = h * 6.0
    i = np.floor(h6).astype(int) % 6
    f = h6 - np.floor(h6)
    p = v * (1 - s)
    q = v * (1 - f * s)
    t = v * (1 - (1 - f) * s)
    
    r_out = np.zeros_like(r)
    g_out = np.zeros_like(g)
    b_out = np.zeros_like(b)
    
    m0 = (i == 0); r_out[m0] = v[m0]; g_out[m0] = t[m0]; b_out[m0] = p[m0]
    m1 = (i == 1); r_out[m1] = q[m1]; g_out[m1] = v[m1]; b_out[m1] = p[m1]
    m2 = (i == 2); r_out[m2] = p[m2]; g_out[m2] = v[m2]; b_out[m2] = t[m2]
    m3 = (i == 3); r_out[m3] = p[m3]; g_out[m3] = q[m3]; b_out[m3] = v[m3]
    m4 = (i == 4); r_out[m4] = t[m4]; g_out[m4] = p[m4]; b_out[m4] = v[m4]
    m5 = (i == 5); r_out[m5] = v[m5]; g_out[m5] = p[m5]; b_out[m5] = q[m5]
    
    result = np.stack([
        np.clip(r_out * 255, 0, 255).astype(np.uint8),
        np.clip(g_out * 255, 0, 255).astype(np.uint8),
        np.clip(b_out * 255, 0, 255).astype(np.uint8),
        a.astype(np.uint8)
    ], axis=2)
    
    result_img = Image.fromarray(result, "RGBA")
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    result_img.save(output_path)
    print(f"  [OK] Saved: {output_path}")


def batch_convert():
    """모든 텍스처를 5가지 색상으로 일괄 변환합니다."""
    
    print("=" * 60)
    print("  FX_TouchParticle_17 텍스처 색상 변환 도구")
    print("=" * 60)
    print()
    
    for tex_name in TEXTURES_TO_CONVERT:
        src_path = os.path.join(TEXTURE_DIR, tex_name)
        
        if not os.path.exists(src_path):
            print(f"[SKIP] 파일 없음: {src_path}")
            continue
        
        name_base, ext = os.path.splitext(tex_name)
        
        # "_Blue" 제거 (있는 경우)
        clean_name = name_base.replace("_Blue", "").replace("_blue", "")
        
        print(f"\n>>> 변환 중: {tex_name}")
        print(f"    원본 경로: {src_path}")
        
        for color_name, target_hue in COLOR_VARIANTS.items():
            out_name = f"{clean_name}_{color_name}{ext}"
            out_path = os.path.join(TEXTURE_DIR, out_name)
            
            print(f"\n  [{color_name}] Hue={target_hue}도 → {out_name}")
            shift_hue_fast(src_path, target_hue, out_path)
    
    print("\n" + "=" * 60)
    print("  변환 완료!")
    print("=" * 60)
    print()
    print("생성된 텍스처 파일들:")
    for tex_name in TEXTURES_TO_CONVERT:
        name_base, ext = os.path.splitext(tex_name)
        clean_name = name_base.replace("_Blue", "").replace("_blue", "")
        for color_name in COLOR_VARIANTS:
            print(f"  - {clean_name}_{color_name}{ext}")
    
    print()
    print("다음 단계:")
    print("  1. Unity에서 Assets > Refresh (Ctrl+R)")
    print("  2. 각 색상별 머티리얼을 복제하여 새 텍스처 연결")
    print("  3. 파티클 프리팹에 새 머티리얼 적용")


def single_convert(input_path, target_hue, output_path):
    """단일 파일 변환"""
    if not os.path.exists(input_path):
        print(f"[ERROR] 파일 없음: {input_path}")
        return
    
    print(f"변환: {input_path} → {output_path} (Hue={target_hue})")
    shift_hue_fast(input_path, float(target_hue), output_path)


if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] == "--input":
        # 단일 파일 모드
        import argparse
        parser = argparse.ArgumentParser(description="텍스처 Hue 시프트 도구")
        parser.add_argument("--input", required=True, help="입력 텍스처 경로")
        parser.add_argument("--target_hue", type=float, required=True, help="목표 Hue (0-360)")
        parser.add_argument("--output", required=True, help="출력 텍스처 경로")
        args = parser.parse_args()
        single_convert(args.input, args.target_hue, args.output)
    else:
        # 일괄 변환 모드
        batch_convert()
