# Texture Tags and Search Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ใส่ Tag ต่อ Texture แล้วค้นจากแถบ Drawable เพื่อเลือก Texture ที่ตรง

**Architecture:** บันทึก Tag ใน `GTexture`; ขยายตัวกรอง `DrawableList`; ให้ `ProjectWindow` เชื่อมผลการค้นหากับรายการ Texture ใน `SelectedDrawable`. ใช้ `TagsEditor` เดิม

**Tech Stack:** C#, WPF, .NET 10, System.Text.Json

**Spec:** `docs/superpowers/specs/2026-11-22-texture-tags-search-design.md`

## Global Constraints

- ใช้ Tag แยกต่อแถว Texture ปกติในแท็บ Textures
- เก็บ Tag ในโปรเจกต์และรักษาไว้เมื่อ Replace หรือคัดลอก Drawable ข้ามเพศ
- ค้นหาใน Addon ปัจจุบัน โดยไม่แยกตัวพิมพ์เล็กใหญ่
- เมื่อค้นเจอ ให้เลือก Drawable แรกและ Texture ที่ตรง แล้วเลือก Texture ที่ตรงอีกครั้งเมื่อเลือก Drawable ผลลัพธ์อื่น

## Review Focus

- โปรเจกต์เก่าที่ไม่มี `Tags` ของ Texture ต้องเปิดและแก้ไข Tag ได้
- Tag ว่างไม่ทำให้ผลค้นหาเพิ่ม
- การลบ Tag ต้องเปลี่ยนผลค้นหา
- Replace Texture ต้องคง Tag
- หลาย Drawable ที่มี Tag เดียวกันต้องเลือก Texture ตาม Drawable ที่ผู้ใช้เลือก

---

### Task 1: Tag metadata and editor

**Files:** `mjxClothTool/Models/Texture/GTexture.cs`, `mjxClothTool/Controls/Drawable/SelectedDrawable.xaml`, `mjxClothTool/Controls/Drawable/SelectedDrawable.xaml.cs`, `mjxClothTool/Controls/Drawable/DrawableList.xaml.cs`, `mjxClothTool.UnitTests/Views/TextureTagsTests.cs`

**Interfaces:** `GTexture.Tags : ObservableCollection<string>`; `DrawableList.GetMatchingTexture(GDrawable drawable, string searchText) : GTexture?`.

- [x] Write tests for tag serialization, search matching and empty tag behavior; verify red.
- [x] Implement metadata, Tag editor, row badges, copy and replacement preservation; verify green.

### Task 2: Search interaction

**Files:** `mjxClothTool/Controls/Drawable/DrawableList.xaml.cs`, `mjxClothTool/Controls/Drawable/SelectedDrawable.xaml.cs`, `mjxClothTool/Views/ProjectWindow.xaml`, `mjxClothTool/Views/ProjectWindow.xaml.cs`.

**Interfaces:** DrawableList search-change event and matching-texture selection method on SelectedDrawable.

- [x] Add a failing search behavior test, then wire search filtering and first result selection.
- [x] Select the matching Texture on each Drawable result; refresh filtered results when tags change.
- [x] Run regression tests and Release build, package, review, and commit.
