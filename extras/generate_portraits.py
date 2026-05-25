#!/usr/bin/env python3
"""
Star Captain — Sci-Fi Character Portrait Generator
Generates 25 male + 25 female 512x512 RGBA PNG portraits with transparent backgrounds.
"""

import cairo
import math
import os

W, H = 512, 512
OUTPUT_DIR = "/sessions/busy-dreamy-gauss/mnt/spacegame/SpaceGame/Assets/Resources/PortraitsNew"
os.makedirs(OUTPUT_DIR, exist_ok=True)

SKIN = [
    {"b": (1.00,0.88,0.74), "s": (0.85,0.70,0.54), "l": (1.00,0.95,0.86)},
    {"b": (0.96,0.77,0.58), "s": (0.80,0.60,0.40), "l": (1.00,0.90,0.74)},
    {"b": (0.90,0.68,0.48), "s": (0.73,0.51,0.30), "l": (1.00,0.82,0.62)},
    {"b": (0.82,0.57,0.36), "s": (0.64,0.40,0.21), "l": (0.94,0.72,0.50)},
    {"b": (0.72,0.46,0.26), "s": (0.54,0.31,0.14), "l": (0.86,0.60,0.38)},
    {"b": (0.60,0.36,0.19), "s": (0.42,0.22,0.09), "l": (0.74,0.50,0.29)},
    {"b": (0.45,0.26,0.12), "s": (0.29,0.15,0.05), "l": (0.60,0.38,0.20)},
    {"b": (0.30,0.17,0.07), "s": (0.17,0.09,0.03), "l": (0.44,0.26,0.12)},
]

HAIR = [
    (0.08,0.05,0.03),(0.32,0.20,0.10),(0.52,0.34,0.18),(0.75,0.55,0.25),
    (0.90,0.78,0.36),(0.62,0.17,0.08),(0.80,0.80,0.82),(0.96,0.96,0.96),
    (0.15,0.32,0.90),(0.62,0.10,0.85),(0.05,0.68,0.60),(0.90,0.22,0.55),
]

EYES = [
    (0.42,0.26,0.10),(0.22,0.46,0.82),(0.22,0.60,0.28),(0.55,0.55,0.60),
    (0.72,0.52,0.08),(0.55,0.18,0.72),(0.05,0.88,0.95),
]

UNIFORMS = [
    {"b":(0.07,0.11,0.24),"t":(0.28,0.56,0.88),"a":(0.16,0.26,0.50)},
    {"b":(0.18,0.20,0.24),"t":(0.58,0.64,0.72),"a":(0.30,0.34,0.40)},
    {"b":(0.20,0.07,0.07),"t":(0.88,0.28,0.18),"a":(0.42,0.13,0.10)},
    {"b":(0.07,0.16,0.12),"t":(0.22,0.72,0.48),"a":(0.12,0.30,0.22)},
    {"b":(0.07,0.14,0.22),"t":(0.18,0.72,0.88),"a":(0.10,0.28,0.42)},
    {"b":(0.05,0.05,0.08),"t":(0.52,0.42,0.82),"a":(0.16,0.12,0.36)},
    {"b":(0.16,0.08,0.22),"t":(0.72,0.30,0.92),"a":(0.32,0.12,0.44)},
    {"b":(0.14,0.14,0.07),"t":(0.62,0.60,0.18),"a":(0.26,0.26,0.10)},
]

def ellipse(ctx, cx, cy, rx, ry):
    ctx.save(); ctx.translate(cx,cy); ctx.scale(rx,ry)
    ctx.arc(0,0,1,0,2*math.pi); ctx.restore()

def sc(ctx, color, alpha=None):
    if alpha is not None: ctx.set_source_rgba(*color, alpha)
    elif len(color)==4: ctx.set_source_rgba(*color)
    else: ctx.set_source_rgb(*color)

def dark(c, f=0.65): return tuple(max(0.0,x*f) for x in c)
def bright(c, f=1.25): return tuple(min(1.0,x*f) for x in c)

def trace_face(ctx, cx, cy, gender):
    rx,ry = (102,112) if gender=="male" else (96,112)
    jsq = 0.82 if gender=="male" else 0.76
    cdrop = 8 if gender=="male" else 3
    ty=cy-ry; chkx=rx*1.02; chky=cy+15
    jawx=rx*jsq; jawy=cy+ry*0.58; chiny=cy+ry*0.96+cdrop
    ctx.move_to(cx,ty)
    ctx.curve_to(cx-rx*0.72,ty+8, cx-chkx,chky-38, cx-chkx,chky)
    ctx.curve_to(cx-chkx,chky+32, cx-jawx,jawy-18, cx-jawx*0.50,chiny)
    ctx.curve_to(cx-jawx*0.28,chiny+cdrop+4, cx+jawx*0.28,chiny+cdrop+4, cx+jawx*0.50,chiny)
    ctx.curve_to(cx+jawx,jawy-18, cx+chkx,chky+32, cx+chkx,chky)
    ctx.curve_to(cx+chkx,chky-38, cx+rx*0.72,ty+8, cx,ty)
    ctx.close_path()

def draw_uniform(ctx, p):
    u=p["uniform"]; cx=256
    ctx.move_to(0,430); ctx.curve_to(0,385,45,355,128,364)
    ctx.curve_to(168,370,192,392,205,430); ctx.line_to(205,512); ctx.line_to(0,512); ctx.close_path()
    sc(ctx,u["b"]); ctx.fill()
    ctx.move_to(512,430); ctx.curve_to(512,385,467,355,384,364)
    ctx.curve_to(344,370,320,392,307,430); ctx.line_to(307,512); ctx.line_to(512,512); ctx.close_path()
    sc(ctx,u["b"]); ctx.fill()
    ctx.move_to(205,430); ctx.curve_to(205,415,228,403,256,401)
    ctx.curve_to(284,403,307,415,307,430); ctx.line_to(307,512); ctx.line_to(205,512); ctx.close_path()
    sc(ctx,u["b"]); ctx.fill()
    ctx.move_to(198,392); ctx.curve_to(198,368,222,352,256,350)
    ctx.curve_to(290,352,314,368,314,392); ctx.curve_to(314,408,292,418,256,420)
    ctx.curve_to(220,418,198,408,198,392); ctx.close_path()
    sc(ctx,u["a"]); ctx.fill()
    ctx.move_to(213,386); ctx.curve_to(213,364,234,354,256,353)
    ctx.curve_to(278,354,299,364,299,386)
    sc(ctx,u["t"]); ctx.set_line_width(2.2); ctx.stroke()
    ctx.move_to(0,400); ctx.curve_to(18,360,72,342,140,356)
    ctx.curve_to(176,364,196,382,196,400); ctx.curve_to(196,414,176,420,145,420)
    ctx.curve_to(76,416,18,412,0,400); ctx.close_path()
    sc(ctx,u["a"]); ctx.fill()
    ctx.move_to(18,364); ctx.curve_to(70,346,135,356,175,372)
    sc(ctx,u["t"]); ctx.set_line_width(2.5); ctx.stroke()
    ctx.move_to(512,400); ctx.curve_to(494,360,440,342,372,356)
    ctx.curve_to(336,364,316,382,316,400); ctx.curve_to(316,414,336,420,367,420)
    ctx.curve_to(436,416,494,412,512,400); ctx.close_path()
    sc(ctx,u["a"]); ctx.fill()
    ctx.move_to(494,364); ctx.curve_to(442,346,377,356,337,372)
    sc(ctx,u["t"]); ctx.set_line_width(2.5); ctx.stroke()
    for i in range(p.get("rank_bars",2)):
        ctx.rectangle(222+i*13,367,9,4)
    sc(ctx,u["t"]); ctx.fill()

def draw_neck(ctx, p):
    sk=p["skin"]
    ctx.move_to(228,382); ctx.curve_to(228,358,236,338,240,328)
    ctx.line_to(272,328); ctx.curve_to(276,338,284,358,284,382)
    ctx.curve_to(276,378,236,378,228,382); ctx.close_path()
    sc(ctx,sk["b"]); ctx.fill()
    ctx.move_to(248,380); ctx.line_to(246,332); ctx.line_to(256,328)
    ctx.line_to(266,332); ctx.line_to(264,380); ctx.close_path()
    sc(ctx,sk["s"],0.22); ctx.fill()

def draw_face(ctx, p):
    sk=p["skin"]; cx,cy=256,p["head_y"]; gd=p["gender"]
    trace_face(ctx,cx,cy,gd); sc(ctx,sk["b"]); ctx.fill()
    rx=104 if gd=="male" else 98
    trace_face(ctx,cx,cy,gd)
    g=cairo.LinearGradient(cx-rx,cy,cx+rx,cy)
    g.add_color_stop_rgba(0.00,*sk["s"],0.42); g.add_color_stop_rgba(0.28,*sk["b"],0.00)
    g.add_color_stop_rgba(0.72,*sk["b"],0.00); g.add_color_stop_rgba(1.00,*sk["s"],0.42)
    ctx.set_source(g); ctx.fill()
    ry=112
    trace_face(ctx,cx,cy,gd)
    g2=cairo.LinearGradient(cx,cy-ry,cx,cy+ry)
    g2.add_color_stop_rgba(0.00,*sk["l"],0.32); g2.add_color_stop_rgba(0.30,*sk["l"],0.00)
    g2.add_color_stop_rgba(0.75,*sk["s"],0.00); g2.add_color_stop_rgba(1.00,*sk["s"],0.28)
    ctx.set_source(g2); ctx.fill()

def draw_ears(ctx, p):
    sk=p["skin"]; cx,cy=256,p["head_y"]
    rx=104 if p["gender"]=="male" else 98; ey=cy+4
    for side in(-1,1):
        ex=cx+side*(rx-3)
        ellipse(ctx,ex,ey,12,18); sc(ctx,sk["b"]); ctx.fill()
        ellipse(ctx,ex+side*1,ey,6,9); sc(ctx,sk["s"]); ctx.fill()

def _male_hair_back(ctx,hc,st,cx,cy,ty):
    if st in(0,1,3,4,7): return
    if st==2:
        ctx.move_to(cx-96,cy-28); ctx.curve_to(cx-103,cy-72,cx-82,ty-18,cx,ty-22)
        ctx.curve_to(cx+82,ty-18,cx+103,cy-72,cx+96,cy-28)
        ctx.curve_to(cx+103,cy-5,cx-103,cy-5,cx-96,cy-28); sc(ctx,hc); ctx.fill()
    elif st==5:
        ctx.move_to(cx-18,cy-75); ctx.line_to(cx-14,cy+95)
        ctx.curve_to(cx-6,cy+102,cx+6,cy+102,cx+14,cy+95)
        ctx.line_to(cx+18,cy-75); sc(ctx,hc); ctx.fill()
    elif st==6:
        ctx.move_to(cx-96,cy-22); ctx.curve_to(cx-103,cy-68,cx-82,ty-14,cx,ty-18)
        ctx.curve_to(cx+82,ty-14,cx+103,cy-68,cx+96,cy-22)
        ctx.curve_to(cx+96,cy+32,cx-96,cy+32,cx-96,cy-22); sc(ctx,hc); ctx.fill()

def _female_hair_back(ctx,hc,st,cx,cy,ty):
    if st==0: return
    if st==1:
        ctx.move_to(cx-96,cy-26); ctx.curve_to(cx-104,cy-70,cx-84,ty-14,cx,ty-18)
        ctx.curve_to(cx+84,ty-14,cx+104,cy-70,cx+96,cy-26)
        ctx.curve_to(cx+100,cy+65,cx+92,cy+88,cx+80,cy+108)
        ctx.curve_to(cx+54,cy+126,cx-54,cy+126,cx-80,cy+108)
        ctx.curve_to(cx-92,cy+88,cx-100,cy+65,cx-96,cy-26); sc(ctx,hc); ctx.fill()
    elif st==2:
        ctx.move_to(cx-96,cy-26); ctx.curve_to(cx-104,cy-70,cx-84,ty-14,cx,ty-18)
        ctx.curve_to(cx+84,ty-14,cx+104,cy-70,cx+96,cy-26)
        ctx.curve_to(cx+108,cy+48,cx+114,cy+140,cx+98,cy+235)
        ctx.curve_to(cx+72,cy+285,cx-72,cy+285,cx-98,cy+235)
        ctx.curve_to(cx-114,cy+140,cx-108,cy+48,cx-96,cy-26); sc(ctx,hc); ctx.fill()
    elif st==3:
        ctx.move_to(cx-96,cy-26); ctx.curve_to(cx-104,cy-70,cx-84,ty-14,cx,ty-18)
        ctx.curve_to(cx+84,ty-14,cx+104,cy-70,cx+96,cy-26)
        ctx.curve_to(cx+96,cy+2,cx-96,cy+2,cx-96,cy-26); sc(ctx,hc); ctx.fill()
        ctx.move_to(cx-22,ty+8); ctx.curve_to(cx-14,ty-50,cx+14,ty-50,cx+22,ty+8)
        ctx.line_to(cx+22,ty+94); ctx.curve_to(cx+16,ty+108,cx-16,ty+108,cx-22,ty+94)
        ctx.close_path(); sc(ctx,hc); ctx.fill()
    elif st in(4,6):
        ctx.move_to(cx-96,cy-26); ctx.curve_to(cx-104,cy-70,cx-84,ty-14,cx,ty-18)
        ctx.curve_to(cx+84,ty-14,cx+104,cy-70,cx+96,cy-26)
        ctx.curve_to(cx+96,cy+2,cx-96,cy+2,cx-96,cy-26); sc(ctx,hc); ctx.fill()
    elif st==5:
        ctx.move_to(cx-96,cy-26); ctx.curve_to(cx-104,cy-70,cx-84,ty-14,cx,ty-18)
        ctx.curve_to(cx+84,ty-14,cx+104,cy-70,cx+96,cy-26)
        ctx.curve_to(cx+110,cy+28,cx+118,cy+88,cx+108,cy+148)
        ctx.curve_to(cx+98,cy+208,cx+78,cy+258,cx+62,cy+292)
        ctx.curve_to(cx+32,cy+308,cx-32,cy+308,cx-62,cy+292)
        ctx.curve_to(cx-78,cy+258,cx-98,cy+208,cx-108,cy+148)
        ctx.curve_to(cx-118,cy+88,cx-110,cy+28,cx-96,cy-26); sc(ctx,hc); ctx.fill()
    elif st==7:
        ctx.move_to(cx,ty-18); ctx.curve_to(cx+84,ty-12,cx+104,cy-70,cx+96,cy-26)
        ctx.curve_to(cx+108,cy+45,cx+102,cy+90,cx+86,cy+120)
        ctx.curve_to(cx+56,cy+138,cx+12,cy+100,cx,cy+78); ctx.close_path()
        sc(ctx,hc); ctx.fill()

def draw_hair_back(ctx, p):
    hc=p["hair_color"]; st=p["hair_style"]; cx,cy=256,p["head_y"]; ty=cy-112
    if p["gender"]=="male": _male_hair_back(ctx,hc,st,cx,cy,ty)
    else: _female_hair_back(ctx,hc,st,cx,cy,ty)

def _male_hair_front(ctx,hc,st,cx,cy,ty):
    if st==0:
        ctx.move_to(cx-96,cy-48); ctx.curve_to(cx-102,cy-84,cx-78,ty+4,cx,ty-4)
        ctx.curve_to(cx+78,ty+4,cx+102,cy-84,cx+96,cy-48)
        ctx.curve_to(cx+102,cy-28,cx-102,cy-28,cx-96,cy-48); ctx.close_path()
        sc(ctx,hc); ctx.fill()
    elif st==1:
        ctx.move_to(cx-102,cy-38); ctx.curve_to(cx-106,cy-82,cx-80,ty-2,cx,ty-5)
        ctx.curve_to(cx+80,ty-2,cx+106,cy-82,cx+102,cy-38)
        ctx.curve_to(cx+106,cy-22,cx-106,cy-22,cx-102,cy-38); ctx.close_path()
        sc(ctx,hc,0.72); ctx.fill()
    elif st==2:
        ctx.move_to(cx-102,cy-28); ctx.curve_to(cx-108,cy-76,cx-84,ty-22,cx,ty-26)
        ctx.curve_to(cx+84,ty-22,cx+108,cy-76,cx+102,cy-28)
        ctx.curve_to(cx+104,cy-8,cx-104,cy-8,cx-102,cy-28); ctx.close_path()
        sc(ctx,hc); ctx.fill()
        for i in range(5):
            sx=cx-40+i*18; ctx.move_to(sx,ty-8+i*3)
            ctx.curve_to(sx+10,ty+22,sx+15,cy-58,sx+5,cy-28)
            sc(ctx,(1,1,1),0.08); ctx.set_line_width(2.0); ctx.stroke()
    elif st==3:
        ctx.move_to(cx-102,cy-34); ctx.line_to(cx-102,ty+10); ctx.line_to(cx-56,ty+4)
        ctx.line_to(cx+56,ty+4); ctx.line_to(cx+102,ty+10); ctx.line_to(cx+102,cy-34)
        ctx.curve_to(cx+104,cy-16,cx-104,cy-16,cx-102,cy-34); ctx.close_path()
        sc(ctx,hc); ctx.fill()
    elif st==4:
        for side in(-1,1):
            ctx.move_to(cx+side*102,cy-28); ctx.line_to(cx+side*102,ty+38)
            ctx.curve_to(cx+side*102,ty+18,cx+side*76,ty+8,cx+side*56,ty+6)
            ctx.line_to(cx+side*56,cy-28); ctx.close_path()
            sc(ctx,hc,0.48); ctx.fill()
        ctx.move_to(cx-56,ty+6); ctx.curve_to(cx-22,ty-32,cx+22,ty-32,cx+56,ty+6)
        ctx.line_to(cx+56,cy-26); ctx.curve_to(cx+22,cy-18,cx-22,cy-18,cx-56,cy-26)
        ctx.close_path(); sc(ctx,hc); ctx.fill()
    elif st==5:
        ctx.move_to(cx-102,cy-18); ctx.curve_to(cx-108,cy-72,cx-86,ty-18,cx,ty-22)
        ctx.curve_to(cx+86,ty-18,cx+108,cy-72,cx+102,cy-18)
        ctx.curve_to(cx+102,cy+2,cx-102,cy+2,cx-102,cy-18); ctx.close_path()
        sc(ctx,hc); ctx.fill()
    elif st==6:
        ctx.move_to(cx-98,cy-24); ctx.curve_to(cx-106,cy-74,cx-84,ty-16,cx,ty-20)
        ctx.curve_to(cx+84,ty-16,cx+106,cy-74,cx+98,cy-24)
        ctx.curve_to(cx+100,cy+28,cx-100,cy+28,cx-98,cy-24); ctx.close_path()
        sc(ctx,hc); ctx.fill()
    elif st==7:
        ctx.move_to(cx-20,cy-34); ctx.curve_to(cx-22,ty-42,cx+22,ty-42,cx+20,cy-34)
        ctx.line_to(cx+20,cy-26); ctx.curve_to(cx+10,cy-18,cx-10,cy-18,cx-20,cy-26)
        ctx.close_path(); sc(ctx,hc); ctx.fill()
        ctx.move_to(cx-4,ty-30); ctx.line_to(cx-2,cy-34)
        sc(ctx,bright(hc,1.35),0.5); ctx.set_line_width(3); ctx.stroke()

def _female_hair_front(ctx,hc,st,cx,cy,ty):
    if st==0:
        ctx.move_to(cx-98,cy-33); ctx.curve_to(cx-104,cy-76,cx-82,ty-6,cx,ty-12)
        ctx.curve_to(cx+82,ty-6,cx+104,cy-76,cx+98,cy-33)
        ctx.curve_to(cx+102,cy-15,cx-102,cy-15,cx-98,cy-33); ctx.close_path()
        sc(ctx,hc); ctx.fill()
        for side in(-1,1):
            ctx.move_to(cx+side*98,cy-28)
            ctx.curve_to(cx+side*108,cy-8,cx+side*112,cy+8,cx+side*106,cy+24)
            ctx.curve_to(cx+side*100,cy+32,cx+side*90,cy+24,cx+side*88,cy+10)
            ctx.curve_to(cx+side*86,cy-8,cx+side*90,cy-18,cx+side*98,cy-28)
            ctx.close_path(); sc(ctx,hc); ctx.fill()
    elif st==1:
        ctx.move_to(cx-98,cy-28); ctx.curve_to(cx-106,cy-76,cx-84,ty-12,cx,ty-16)
        ctx.curve_to(cx+84,ty-12,cx+106,cy-76,cx+98,cy-28)
        ctx.curve_to(cx+102,cy+52,cx+96,cy+92,cx+88,cy+114)
        ctx.curve_to(cx+60,cy+128,cx-60,cy+128,cx-88,cy+114)
        ctx.curve_to(cx-96,cy+92,cx-102,cy+52,cx-98,cy-28); ctx.close_path()
        sc(ctx,hc); ctx.fill()
    elif st==2:
        ctx.move_to(cx-98,cy-28); ctx.curve_to(cx-106,cy-76,cx-84,ty-12,cx,ty-16)
        ctx.curve_to(cx+84,ty-12,cx+106,cy-76,cx+98,cy-28)
        ctx.curve_to(cx+110,cy+52,cx+117,cy+148,cx+102,cy+242)
        ctx.curve_to(cx+74,cy+292,cx-74,cy+292,cx-102,cy+242)
        ctx.curve_to(cx-117,cy+148,cx-110,cy+52,cx-98,cy-28); ctx.close_path()
        sc(ctx,hc); ctx.fill()
        ctx.move_to(cx,ty-16); ctx.line_to(cx,cy-18)
        sc(ctx,(0,0,0),0.14); ctx.set_line_width(2.2); ctx.stroke()
    elif st==3:
        ctx.move_to(cx-98,cy-28); ctx.curve_to(cx-106,cy-76,cx-84,ty-12,cx,ty-16)
        ctx.curve_to(cx+84,ty-12,cx+106,cy-76,cx+98,cy-28)
        ctx.curve_to(cx+98,cy-4,cx-98,cy-4,cx-98,cy-28); ctx.close_path()
        sc(ctx,hc); ctx.fill()
        ctx.move_to(cx-24,ty+6); ctx.curve_to(cx-16,ty-52,cx+16,ty-52,cx+24,ty+6)
        ctx.line_to(cx+24,ty+96); ctx.curve_to(cx+18,ty+112,cx-18,ty+112,cx-24,ty+96)
        ctx.close_path(); sc(ctx,hc); ctx.fill()
        ctx.rectangle(cx-24,ty+10,48,10); sc(ctx,dark(hc,0.52)); ctx.fill()
    elif st==4:
        ctx.move_to(cx-98,cy-28); ctx.curve_to(cx-106,cy-76,cx-84,ty-10,cx,ty-14)
        ctx.curve_to(cx+84,ty-10,cx+106,cy-76,cx+98,cy-28)
        ctx.curve_to(cx+98,cy-4,cx-98,cy-4,cx-98,cy-28); ctx.close_path()
        sc(ctx,hc); ctx.fill()
        ellipse(ctx,cx,ty-24,32,26); sc(ctx,hc); ctx.fill()
        ctx.arc(cx,ty-24,30,0,2*math.pi); sc(ctx,dark(hc,0.68),0.45)
        ctx.set_line_width(2.2); ctx.stroke()
    elif st==5:
        ctx.move_to(cx-98,cy-28); ctx.curve_to(cx-106,cy-76,cx-84,ty-12,cx,ty-16)
        ctx.curve_to(cx+84,ty-12,cx+106,cy-76,cx+98,cy-28)
        ctx.curve_to(cx+112,cy+22,cx+120,cy+82,cx+110,cy+142)
        ctx.curve_to(cx+100,cy+202,cx+80,cy+255,cx+64,cy+292)
        ctx.curve_to(cx+34,cy+308,cx-34,cy+308,cx-64,cy+292)
        ctx.curve_to(cx-80,cy+255,cx-100,cy+202,cx-110,cy+142)
        ctx.curve_to(cx-120,cy+82,cx-112,cy+22,cx-98,cy-28); ctx.close_path()
        sc(ctx,hc); ctx.fill()
        for off in(-28,28):
            ctx.move_to(cx+off,cy+12); ctx.curve_to(cx+off+22,cy+55,cx+off-18,cy+96,cx+off+12,cy+140)
            sc(ctx,(0,0,0),0.09); ctx.set_line_width(2.8); ctx.stroke()
    elif st==6:
        ctx.move_to(cx-98,cy-28); ctx.curve_to(cx-106,cy-76,cx-84,ty-12,cx,ty-16)
        ctx.curve_to(cx+84,ty-12,cx+106,cy-76,cx+98,cy-28)
        ctx.curve_to(cx+98,cy+18,cx-98,cy+18,cx-98,cy-28); ctx.close_path()
        sc(ctx,hc); ctx.fill()
        hcd=dark(hc,0.72)
        for side in(-1,1):
            bx=cx+side*54
            ctx.move_to(bx-9,cy+10); ctx.curve_to(bx-7,cy+55,bx+7,cy+98,bx-6,cy+148)
            ctx.curve_to(bx-7,cy+166,bx+7,cy+166,bx+6,cy+148)
            ctx.curve_to(bx+7,cy+98,bx-7,cy+55,bx+9,cy+10); ctx.close_path()
            sc(ctx,hc); ctx.fill()
            for y in range(int(cy+14),int(cy+152),18):
                ctx.move_to(bx-9,y); ctx.line_to(bx+9,y+9)
                sc(ctx,hcd,0.55); ctx.set_line_width(1.6); ctx.stroke()
                ctx.move_to(bx+9,y+5); ctx.line_to(bx-9,y+14); ctx.stroke()
    elif st==7:
        ctx.move_to(cx,ty-16); ctx.curve_to(cx+84,ty-12,cx+106,cy-76,cx+98,cy-28)
        ctx.curve_to(cx+110,cy+42,cx+104,cy+88,cx+88,cy+118)
        ctx.curve_to(cx+58,cy+136,cx+14,cy+98,cx,cy+76); ctx.close_path()
        sc(ctx,hc); ctx.fill()
        ctx.move_to(cx,ty-16); ctx.curve_to(cx-62,ty-2,cx-106,cy-82,cx-98,cy-30)
        ctx.curve_to(cx-99,cy-8,cx-4,cy-4,cx,cy-6); ctx.close_path()
        sc(ctx,hc,0.42); ctx.fill()

def draw_hair_front(ctx, p):
    hc=p["hair_color"]; st=p["hair_style"]; cx,cy=256,p["head_y"]; ty=cy-112
    if p["gender"]=="male": _male_hair_front(ctx,hc,st,cx,cy,ty)
    else: _female_hair_front(ctx,hc,st,cx,cy,ty)

def draw_eyebrows(ctx, p):
    hc=p["hair_color"]; gd=p["gender"]; cx,cy=256,p["head_y"]
    ey=cy-12; by=ey-17; hr,hg,hb=hc
    br=max(0.0,hr*0.60); bg=max(0.0,hg*0.50); bb=max(0.0,hb*0.42)
    if (hr+hg+hb)/3>0.70: br,bg,bb=0.28,0.22,0.14
    for side in(-1,1):
        ex=cx+side*50
        if gd=="male":
            ctx.move_to(ex-18,by+4); ctx.curve_to(ex-4,by+1,ex+4,by-1,ex+18,by+3)
            ctx.line_to(ex+18,by); ctx.curve_to(ex+4,by-4,ex-4,by-2,ex-18,by+1)
            ctx.close_path()
        else:
            pk=by-5
            ctx.move_to(ex-17,by+2); ctx.curve_to(ex-side*2,pk,ex+side*4,pk-1,ex+17,by+2)
            ctx.line_to(ex+17,by+4); ctx.curve_to(ex+side*4,pk+2,ex-side*2,pk+2,ex-17,by+4)
            ctx.close_path()
        sc(ctx,(br,bg,bb)); ctx.fill()

def draw_eyes(ctx, p):
    ec=p["eye_color"]; sk=p["skin"]; cx,cy=256,p["head_y"]
    ey=cy-12; is_cyber=(p["eye_idx"]==6)
    for side in(-1,1):
        ex=cx+side*50
        ellipse(ctx,ex,ey,24,15); sc(ctx,sk["s"],0.28); ctx.fill()
        ellipse(ctx,ex,ey,20,11); sc(ctx,(0.96,0.94,0.90)); ctx.fill()
        ctx.arc(ex,ey,8.5,0,2*math.pi); sc(ctx,ec); ctx.fill()
        if is_cyber:
            ctx.arc(ex,ey,8.5,0,2*math.pi); sc(ctx,ec,0.85); ctx.set_line_width(2.2); ctx.stroke()
            ctx.arc(ex,ey,11.5,0,2*math.pi); sc(ctx,ec,0.32); ctx.set_line_width(1.8); ctx.stroke()
        ctx.arc(ex,ey,4.2,0,2*math.pi); sc(ctx,(0.04,0.03,0.03)); ctx.fill()
        ctx.arc(ex-2.5,ey-2.5,2.2,0,2*math.pi); sc(ctx,(1,1,1),0.88); ctx.fill()
        ctx.arc(ex+1.6,ey+1.6,1.1,0,2*math.pi); sc(ctx,(1,1,1),0.50); ctx.fill()
        ctx.save(); ctx.translate(ex,ey); ctx.scale(20,11)
        ctx.arc(0,0,1,math.pi,0); ctx.restore()
        sc(ctx,(0.04,0.03,0.03),0.88); ctx.set_line_width(2.2); ctx.stroke()
        ctx.save(); ctx.translate(ex,ey); ctx.scale(20,11)
        ctx.arc(0,0,1,0,math.pi); ctx.restore()
        sc(ctx,(0.04,0.03,0.03),0.28); ctx.set_line_width(1.0); ctx.stroke()
        for ang in(0.18,0.35,0.52,0.70,0.86):
            th=math.pi*ang; lx=ex+math.cos(math.pi-th)*20; ly=ey-math.sin(th)*11
            ctx.move_to(lx,ly); ctx.line_to(lx+math.cos(math.pi-th)*5,ly-math.sin(th)*4.5)
            sc(ctx,(0.04,0.03,0.03),0.75); ctx.set_line_width(1.5); ctx.stroke()

def draw_nose(ctx, p):
    sk=p["skin"]; cx,cy=256,p["head_y"]; ny=cy+30; nw=12
    for side in(-1,1):
        ctx.move_to(cx+side*5,cy-4); ctx.curve_to(cx+side*7,cy+10,cx+side*nw,ny-6,cx+side*nw,ny)
        sc(ctx,sk["s"],0.52); ctx.set_line_width(1.5); ctx.stroke()
    for side in(-1,1):
        ctx.arc(cx+side*(nw-2),ny+4,5.2,0,2*math.pi); sc(ctx,sk["s"],0.58); ctx.fill()
    ctx.arc(cx,ny-1,4.2,0,2*math.pi); sc(ctx,sk["l"],0.28); ctx.fill()

def draw_mouth(ctx, p):
    sk=p["skin"]; gd=p["gender"]; expr=p["expression"]; cx,cy=256,p["head_y"]
    my=cy+60; mw=22 if gd=="male" else 20
    lr=min(1.0,sk["b"][0]*0.84); lg=min(1.0,sk["b"][1]*0.66); lb=min(1.0,sk["b"][2]*0.60)
    ctx.move_to(cx-mw,my)
    if expr=="slight_smile":
        ctx.curve_to(cx-9,my-4,cx-4,my-7,cx,my-6); ctx.curve_to(cx+4,my-7,cx+9,my-4,cx+mw,my)
        ctx.curve_to(cx+9,my+2,cx-9,my+2,cx-mw,my)
    elif expr=="neutral":
        ctx.curve_to(cx-7,my-3,cx-3,my-5,cx,my-5); ctx.curve_to(cx+3,my-5,cx+7,my-3,cx+mw,my)
        ctx.curve_to(cx+7,my+2,cx-7,my+2,cx-mw,my)
    else:
        ctx.line_to(cx,my); ctx.line_to(cx+mw,my)
        ctx.curve_to(cx+7,my+3,cx-7,my+3,cx-mw,my)
    ctx.close_path(); sc(ctx,(lr,lg,lb)); ctx.fill()
    ctx.move_to(cx-mw,my)
    if expr=="slight_smile":
        ctx.curve_to(cx-8,my+5,cx-3,my+10,cx,my+10); ctx.curve_to(cx+3,my+10,cx+8,my+5,cx+mw,my)
        ctx.curve_to(cx+8,my+2,cx-8,my+2,cx-mw,my)
    elif expr=="neutral":
        ctx.curve_to(cx-8,my+6,cx-3,my+11,cx,my+11); ctx.curve_to(cx+3,my+11,cx+8,my+6,cx+mw,my)
        ctx.curve_to(cx+8,my+2,cx-8,my+2,cx-mw,my)
    else:
        ctx.curve_to(cx-8,my+7,cx-3,my+12,cx,my+12); ctx.curve_to(cx+3,my+12,cx+8,my+7,cx+mw,my)
        ctx.curve_to(cx+8,my+3,cx-8,my+3,cx-mw,my)
    ctx.close_path(); sc(ctx,(min(1.0,lr*1.12),min(1.0,lg*1.06),lb)); ctx.fill()
    ctx.move_to(cx-mw,my); ctx.line_to(cx+mw,my)
    sc(ctx,(lr*0.58,lg*0.48,lb*0.46),0.62); ctx.set_line_width(1.2); ctx.stroke()
    ctx.move_to(cx-7,my+7); ctx.curve_to(cx-3,my+6,cx+3,my+6,cx+7,my+7)
    sc(ctx,sk["l"],0.28); ctx.set_line_width(2.8); ctx.stroke()

def draw_accessories(ctx, p):
    acc=p["accessory"]; cx,cy=256,p["head_y"]
    if acc=="comm":
        ex=cx+104; ey=cy+4
        ctx.arc(ex,ey,9,0,2*math.pi); sc(ctx,(0.22,0.26,0.30)); ctx.fill()
        ctx.arc(ex,ey,9,0,2*math.pi); sc(ctx,(0.50,0.72,1.00),0.82); ctx.set_line_width(1.8); ctx.stroke()
        ctx.arc(ex+2,ey-2,2.8,0,2*math.pi); sc(ctx,(0.28,0.70,1.00)); ctx.fill()
        ctx.move_to(ex+7,ey-6); ctx.line_to(ex+16,ey-18)
        sc(ctx,(0.60,0.82,1.00),0.88); ctx.set_line_width(1.6); ctx.stroke()
    elif acc=="cyber_eye":
        ex=cx-50; ey=cy-12
        ctx.arc(ex,ey,17,0,2*math.pi); sc(ctx,(0.38,0.42,0.48),0.62); ctx.set_line_width(2.8); ctx.stroke()
        ctx.move_to(ex-15,ey); ctx.line_to(ex-34,ey+6); ctx.line_to(ex-44,ey-9)
        sc(ctx,(0.38,0.42,0.48),0.72); ctx.set_line_width(1.8); ctx.stroke()
        ctx.arc(ex-44,ey-9,3.5,0,2*math.pi); sc(ctx,(0.10,0.88,0.52)); ctx.fill()
    elif acc=="scar":
        sk=p["skin"]; sr=max(0.0,sk["s"][0]*0.72); sg=max(0.0,sk["s"][1]*0.62); sb=max(0.0,sk["s"][2]*0.58)
        ctx.move_to(cx+50,cy-32); ctx.curve_to(cx+57,cy-10,cx+62,cy+14,cx+54,cy+42)
        sc(ctx,(sr,sg,sb),0.78); ctx.set_line_width(2.8); ctx.stroke()
        ctx.move_to(cx+45,cy-30); ctx.curve_to(cx+52,cy-8,cx+57,cy+16,cx+49,cy+40)
        sc(ctx,(sr,sg,sb),0.38); ctx.set_line_width(1.2); ctx.stroke()
    elif acc=="visor":
        ctx.move_to(cx-80,cy-14); ctx.curve_to(cx-80,cy-20,cx-62,cy-26,cx-30,cy-25)
        ctx.line_to(cx+30,cy-25); ctx.curve_to(cx+62,cy-26,cx+80,cy-20,cx+80,cy-14)
        ctx.curve_to(cx+80,cy-7,cx+62,cy-2,cx+30,cy-1)
        ctx.line_to(cx-30,cy-1); ctx.curve_to(cx-62,cy-2,cx-80,cy-7,cx-80,cy-14); ctx.close_path()
        sc(ctx,(0.18,0.55,0.92),0.30); ctx.fill_preserve()
        sc(ctx,(0.28,0.68,1.00),0.72); ctx.set_line_width(1.6); ctx.stroke()
        ctx.move_to(cx-62,cy-21); ctx.curve_to(cx-40,cy-23,cx-18,cy-23,cx+2,cy-22)
        sc(ctx,(1,1,1),0.36); ctx.set_line_width(2.8); ctx.stroke()
        ctx.rectangle(cx-68,cy-20,20,4); sc(ctx,(0.50,1.00,0.50),0.52); ctx.fill()
        ctx.rectangle(cx+48,cy-20,20,4); sc(ctx,(1.00,0.50,0.50),0.52); ctx.fill()

MALE_PARAMS = [
    (0,0,0,1,0,"none","neutral",2),(3,1,1,0,2,"comm","slight_smile",3),
    (6,9,2,6,5,"none","serious",4),(1,4,3,1,1,"visor","neutral",1),
    (5,2,4,2,3,"none","slight_smile",2),(2,5,5,4,7,"scar","serious",3),
    (7,7,6,3,4,"none","neutral",2),(4,8,7,5,6,"comm","slight_smile",1),
    (0,6,0,0,2,"none","serious",4),(3,10,1,6,0,"cyber_eye","serious",2),
    (1,3,2,2,4,"none","slight_smile",3),(6,0,3,4,7,"scar","neutral",2),
    (2,11,4,1,5,"none","slight_smile",1),(5,4,5,3,1,"visor","serious",3),
    (7,1,6,5,3,"none","neutral",4),(0,8,7,0,0,"comm","slight_smile",2),
    (4,2,0,6,2,"none","serious",3),(3,7,1,2,6,"scar","neutral",1),
    (1,5,2,1,7,"none","slight_smile",4),(6,3,3,4,0,"cyber_eye","serious",2),
    (2,9,4,5,3,"visor","slight_smile",3),(5,0,5,3,4,"none","neutral",2),
    (7,6,6,1,1,"comm","slight_smile",1),(0,10,7,0,5,"none","serious",4),
    (4,4,0,2,2,"scar","neutral",2),
]

FEMALE_PARAMS = [
    (0,4,2,1,0,"none","slight_smile",2),(3,0,1,0,2,"comm","neutral",3),
    (6,9,5,6,5,"none","slight_smile",4),(1,2,0,2,1,"visor","serious",1),
    (5,11,3,4,3,"none","neutral",2),(2,5,4,1,7,"none","slight_smile",3),
    (7,7,6,5,4,"scar","neutral",2),(4,8,7,3,6,"comm","serious",1),
    (0,1,2,4,2,"none","slight_smile",4),(3,10,0,6,0,"cyber_eye","serious",2),
    (1,4,5,1,4,"none","slight_smile",3),(6,3,3,2,7,"scar","serious",2),
    (2,6,1,0,5,"none","neutral",1),(5,0,4,3,1,"visor","slight_smile",3),
    (7,4,6,4,3,"none","neutral",4),(0,9,2,5,0,"comm","slight_smile",2),
    (4,2,7,6,2,"none","serious",3),(3,8,0,1,6,"none","neutral",1),
    (1,7,5,2,7,"scar","slight_smile",4),(6,5,1,4,0,"cyber_eye","serious",2),
    (2,11,3,3,3,"visor","neutral",3),(5,1,4,5,4,"none","slight_smile",2),
    (7,3,6,1,1,"comm","neutral",1),(0,10,2,0,5,"none","serious",4),
    (4,6,7,2,2,"none","slight_smile",2),
]

def build(row, gender):
    sk,hr,hs,ey,un,acc,expr,rank=row
    return {"gender":gender,"skin":SKIN[sk],"hair_color":HAIR[hr],"hair_style":hs,
            "eye_idx":ey,"eye_color":EYES[ey],"uniform":UNIFORMS[un],
            "accessory":acc,"expression":expr,"rank_bars":rank,"head_y":196}

def render(p):
    sur=cairo.ImageSurface(cairo.FORMAT_ARGB32,W,H)
    ctx=cairo.Context(sur)
    ctx.set_operator(cairo.OPERATOR_CLEAR); ctx.paint()
    ctx.set_operator(cairo.OPERATOR_OVER)
    draw_hair_back(ctx,p); draw_uniform(ctx,p); draw_neck(ctx,p)
    draw_ears(ctx,p); draw_face(ctx,p); draw_eyebrows(ctx,p)
    draw_eyes(ctx,p); draw_nose(ctx,p); draw_mouth(ctx,p)
    draw_hair_front(ctx,p); draw_accessories(ctx,p)
    return sur

if __name__=="__main__":
    total=0
    for i,row in enumerate(MALE_PARAMS,1):
        p=build(row,"male"); sur=render(p)
        sur.write_to_png(f"{OUTPUT_DIR}/male_{i}.png")
        print(f"  male_{i}.png")
        total+=1
    for i,row in enumerate(FEMALE_PARAMS,1):
        p=build(row,"female"); sur=render(p)
        sur.write_to_png(f"{OUTPUT_DIR}/female_{i}.png")
        print(f"  female_{i}.png")
        total+=1
    print(f"\nDone: {total} portraits -> {OUTPUT_DIR}")
