#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Собрать ключи колонок из [Display] по файлам моделей.

Ключ такой же, как у LocalizedColumnHeaders: ИмяКласса_ИмяСвойства.
В колонку ShortName попадает Display.ShortName, а если его нет — Display.Name.
Свойства с AutoGenerateField = false пропускаются.

Запуск из каталога моделей (или корня решения):

    python collect_display_keys.py C:\\src\\Store -o display-keys.csv

Уже готовый английский подставляется из resx, пустые ячейки English остаются на перевод:

    python collect_display_keys.py C:\\src\\Store --english Strings.en.resx -o display-keys.csv

Файл CSV в кодировке UTF-8 с BOM, разделитель «;», чтобы Excel на русской Windows открыл текст колонками.
"""

from __future__ import annotations

import argparse
import csv
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


TYPE_WORDS = {"class", "struct", "interface", "enum", "record"}
MODIFIERS = {
    "public",
    "private",
    "protected",
    "internal",
    "abstract",
    "sealed",
    "static",
    "partial",
    "new",
    "unsafe",
    "file",
    "readonly",
    "ref",
    "virtual",
    "override",
    "async",
    "extern",
    "volatile",
    "required",
    "sealed",
}
SKIP_DIRS = {"bin", "obj", ".git", ".vs", "packages", "node_modules", "TestResults"}
DISPLAY_NAMES = {"Display", "DisplayAttribute"}


def main(argv):
    parser = argparse.ArgumentParser(description="Собрать ключи и ShortName из [Display] моделей.")
    parser.add_argument("paths", nargs="*", type=Path, help="Каталог или файл с моделями")
    parser.add_argument("-o", "--output", type=Path, default=Path("display-keys.csv"), help="Куда записать CSV")
    parser.add_argument("--english", type=Path, help="Strings.en.resx: подставить уже сделанный перевод")
    parser.add_argument("--self-test", action="store_true", help="Проверить разбор на встроенном примере")
    args = parser.parse_args(argv)

    if args.self_test:
        return run_self_test()

    if not args.paths:
        parser.error("укажите каталог с моделями")

    english = read_resx(args.english) if args.english else {}
    rows = []
    for path in args.paths:
        if not path.exists():
            print("нет пути: " + str(path), file=sys.stderr)
            return 2
        rows.extend(scan_path(path))

    rows.sort(key=lambda row: (row["Key"], row["File"]))
    warn_duplicates(rows)
    for row in rows:
        row["English"] = english.get(row["Key"], "")

    write_csv(args.output, rows)
    missing = sum(1 for row in rows if not row["English"])
    print("Собрано {0}. Без перевода: {1}. Файл: {2}".format(len(rows), missing, args.output))
    return 0


def scan_path(path: Path):
    files = [path] if path.is_file() else sorted(
        item
        for item in path.rglob("*.cs")
        if item.is_file() and not any(part in SKIP_DIRS for part in item.parts)
        and not item.name.endswith(".Designer.cs")
        and not item.name.endswith(".g.cs")
    )
    rows = []
    root = path if path.is_dir() else path.parent
    for file in files:
        text = file.read_text(encoding="utf-8-sig", errors="replace")
        for found in scan_source(strip_preprocessor(text)):
            found["File"] = str(file.relative_to(root)) if file.is_relative_to(root) else str(file)
            rows.append(found)
    return rows


def strip_preprocessor(src: str) -> str:
    lines = []
    for line in src.splitlines(keepends=True):
        if line.lstrip().startswith("#"):
            lines.append("\n" if line.endswith("\n") else "")
        else:
            lines.append(line)
    return "".join(lines)


def scan_source(src: str):
    src = strip_comments(src)
    rows = []
    frames = [("file", None)]
    file_namespace = ""
    i = 0
    n = len(src)

    while i < n:
        i = skip_ws(src, i)
        if i >= n:
            break

        if src[i] == "{":
            frames.append(("block", None))
            i += 1
            continue
        if src[i] == "}":
            if len(frames) > 1:
                frames.pop()
            i += 1
            continue

        if src.startswith(("'", '"', "@"), i) or src.startswith('$"', i) or src.startswith('$@"', i) or src.startswith('@$"', i):
            i = skip_string(src, i)
            continue

        ident, after = read_ident(src, i)
        if ident and frame_accepts_type(frames):
            parsed = try_parse_namespace(src, i)
            if parsed is not None:
                name, i, brace = parsed
                if brace:
                    frames.append(("namespace", name))
                    i += 1
                else:
                    file_namespace = name
                continue
            parsed = try_parse_type(src, i)
            if parsed is not None:
                kind, name, i = parsed
                frames.append((kind, name))
                i += 1
                continue

        if src[i] == "[" and in_type(frames) and previous_member_boundary(src, i):
            attrs, j = read_attribute_group(src, i)
            j = skip_ws(src, j)
            if lookahead_type(src, j):
                i = j
                continue
            member, i, term = read_member(src, j)
            if term in ("{", "=>", ";"):
                ns, class_name = namespace_of(frames, file_namespace)
                row = row_from_member(attrs, member, class_name, ns)
                if row is not None:
                    rows.append(row)
            continue

        i = after if ident else i + 1

    return rows


def frame_accepts_type(frames):
    return frames and frames[-1][0] in ("file", "namespace", "class", "struct", "interface", "record")


def in_type(frames):
    return frames and frames[-1][0] in ("class", "struct", "interface", "record")


def previous_member_boundary(src, index):
    j = index - 1
    while j >= 0 and src[j] in " \t\r\n":
        j -= 1
    return j < 0 or src[j] in "{;}"


def try_parse_namespace(src, i):
    ident, j = read_ident(src, i)
    if ident != "namespace":
        return None
    j = skip_ws(src, j)
    name, j = read_dotted(src, j)
    j = skip_ws(src, j)
    if j < len(src) and src[j] == ";":
        return name, j + 1, False
    if j < len(src) and src[j] == "{":
        return name, j, True
    return None


def try_parse_type(src, i):
    j = i
    while True:
        j = skip_ws(src, j)
        ident, k = read_ident(src, j)
        if ident in MODIFIERS:
            j = k
            continue
        break
    if ident == "record":
        kind = "record"
        j = skip_ws(src, k)
        nxt, k2 = read_ident(src, j)
        if nxt in ("class", "struct"):
            j = k2
        else:
            j = k
    elif ident in TYPE_WORDS:
        kind = ident
        j = k
    else:
        return None

    j = skip_ws(src, j)
    name, j = read_ident(src, j)
    if not name:
        return None
    end = find_type_body(src, j)
    if end is None or src[end] != "{":
        return None
    return kind, name, end


def find_type_body(src, i):
    depth_angle = 0
    depth_paren = 0
    depth_brack = 0
    while i < len(src):
        step = skip_string_or_none(src, i)
        if step is not None:
            i = step
            continue
        c = src[i]
        if c == "<":
            depth_angle += 1
        elif c == ">" and depth_angle:
            depth_angle -= 1
        elif c == "(":
            depth_paren += 1
        elif c == ")" and depth_paren:
            depth_paren -= 1
        elif c == "[":
            depth_brack += 1
        elif c == "]" and depth_brack:
            depth_brack -= 1
        elif c in "{;" and depth_angle == 0 and depth_paren == 0 and depth_brack == 0:
            return i
        i += 1
    return None


def lookahead_type(src, i):
    return try_parse_type(src, i) is not None


def read_attribute_group(src, i):
    attrs = []
    while i < len(src) and src[i] == "[":
        body, i = read_bracket(src, i)
        attrs.extend(split_attributes(body))
        i = skip_ws(src, i)
    return attrs, i


def read_bracket(src, i):
    # i на «[»
    i += 1
    start = i
    depth = 1
    while i < len(src) and depth:
        step = skip_string_or_none(src, i)
        if step is not None:
            i = step
            continue
        if src[i] == "[":
            depth += 1
        elif src[i] == "]":
            depth -= 1
            if depth == 0:
                return src[start:i], i + 1
        i += 1
    return src[start:i], i


def split_attributes(body):
    parts = split_top_commas(body)
    found = []
    for part in parts:
        name, args = split_name_args(part)
        simple = simple_attribute_name(name)
        if simple in DISPLAY_NAMES:
            found.append(parse_named_args(args))
    return found


def split_name_args(body):
    depth = 0
    i = 0
    while i < len(body):
        step = skip_string_or_none(body, i)
        if step is not None:
            i = step
            continue
        if body[i] == "(" and depth == 0:
            name = body[:i]
            args, _ = read_paren_inner(body, i)
            return name, args
        if body[i] == "(":
            depth += 1
        elif body[i] == ")" and depth:
            depth -= 1
        i += 1
    return body, ""


def read_paren_inner(src, i):
    # i на «(»
    i += 1
    start = i
    depth = 1
    while i < len(src) and depth:
        step = skip_string_or_none(src, i)
        if step is not None:
            i = step
            continue
        if src[i] == "(":
            depth += 1
        elif src[i] == ")":
            depth -= 1
            if depth == 0:
                return src[start:i], i + 1
        i += 1
    return src[start:i], i


def simple_attribute_name(name):
    text = name.strip()
    if text.startswith("global::"):
        text = text[len("global::"):]
    cut = None
    i = 0
    while i < len(text):
        if text.startswith("::", i):
            i += 2
            continue
        if text[i] == ":":
            cut = i + 1
        i += 1
    if cut is not None:
        text = text[cut:]
    text = text.strip().split(".")[-1]
    return text


def parse_named_args(args):
    values = {}
    for part in split_top_commas(args):
        if "=" not in part:
            continue
        key, raw = part.split("=", 1)
        values[key.strip()] = parse_value(raw.strip())
    return values


def parse_value(raw):
    if raw.startswith('@"') or raw.startswith('"'):
        return decode_string(raw)
    if raw.lower() == "true":
        return True
    if raw.lower() == "false":
        return False
    return raw


def decode_string(raw):
    verbatim = raw.startswith('@"')
    if verbatim:
        body = raw[2:]
    else:
        body = raw[1:]
    if body.endswith('"'):
        body = body[:-1]
    if verbatim:
        return body.replace('""', '"')
    out = []
    i = 0
    while i < len(body):
        if body[i] != "\\":
            out.append(body[i])
            i += 1
            continue
        i += 1
        if i >= len(body):
            break
        esc = body[i]
        mapping = {"n": "\n", "r": "\r", "t": "\t", "\\": "\\", '"': '"', "'": "'", "0": "\0"}
        if esc in mapping:
            out.append(mapping[esc])
            i += 1
        elif esc == "u" and i + 4 < len(body):
            out.append(chr(int(body[i + 1:i + 5], 16)))
            i += 5
        else:
            out.append(esc)
            i += 1
    return "".join(out)


def split_top_commas(text):
    parts = []
    start = 0
    depth_paren = 0
    depth_angle = 0
    depth_brack = 0
    i = 0
    while i < len(text):
        step = skip_string_or_none(text, i)
        if step is not None:
            i = step
            continue
        c = text[i]
        if c == "(":
            depth_paren += 1
        elif c == ")" and depth_paren:
            depth_paren -= 1
        elif c == "<":
            depth_angle += 1
        elif c == ">" and depth_angle:
            depth_angle -= 1
        elif c == "[":
            depth_brack += 1
        elif c == "]" and depth_brack:
            depth_brack -= 1
        elif c == "," and depth_paren == 0 and depth_angle == 0 and depth_brack == 0:
            parts.append(text[start:i])
            start = i + 1
        i += 1
    tail = text[start:]
    if tail.strip():
        parts.append(tail)
    return parts


def read_member(src, i):
    start = i
    while i < len(src):
        step = skip_string_or_none(src, i)
        if step is not None:
            i = step
            continue
        if src.startswith("=>", i):
            return src[start:i], i + 2, "=>"
        if src[i] in "{;":
            return src[start:i], i, src[i]
        i += 1
    return src[start:i], i, ""


def row_from_member(attrs, member, class_name, namespace):
    if not attrs or not class_name:
        return None
    chosen = None
    for args in attrs:
        if args.get("AutoGenerateField") is False:
            return None
        chosen = args
    if chosen is None:
        return None
    name = property_name(member)
    if not name:
        return None
    short = chosen.get("ShortName")
    display_name = chosen.get("Name")
    if isinstance(short, str) and short.strip():
        caption = short
        source = "ShortName"
    elif isinstance(display_name, str) and display_name.strip():
        caption = display_name
        source = "Name"
    else:
        return None
    return {
        "Key": class_name + "_" + name,
        "ShortName": caption,
        "Source": source,
        "English": "",
        "Class": class_name,
        "Property": name,
        "Namespace": namespace,
        "File": "",
    }


def namespace_of(frames, file_namespace):
    spaces = [name for kind, name in frames if kind == "namespace" and name]
    classes = [name for kind, name in frames if kind in ("class", "struct", "interface", "record") and name]
    ns = ".".join(spaces) if spaces else file_namespace
    class_name = classes[-1] if classes else None
    return (ns, class_name)


def property_name(member):
    if has_word(member, "event") or has_word(member, "delegate") or has_word(member, "operator"):
        return None
    if has_word(member, "this"):
        return None
    if has_paren(member):
        return None
    cleaned = strip_generics_and_arrays(member)
    idents = []
    i = 0
    while i < len(cleaned):
        ident, j = read_ident(cleaned, i)
        if ident:
            idents.append(ident)
            i = j
        else:
            i += 1
    if not idents:
        return None
    return idents[-1]


def has_word(text, word):
    i = 0
    while i < len(text):
        ident, j = read_ident(text, i)
        if ident == word:
            return True
        i = j if ident else i + 1
    return False


def has_paren(text):
    depth_angle = 0
    depth_brack = 0
    i = 0
    while i < len(text):
        step = skip_string_or_none(text, i)
        if step is not None:
            i = step
            continue
        c = text[i]
        if c == "<":
            depth_angle += 1
        elif c == ">" and depth_angle:
            depth_angle -= 1
        elif c == "[":
            depth_brack += 1
        elif c == "]" and depth_brack:
            depth_brack -= 1
        elif c == "(" and depth_angle == 0 and depth_brack == 0:
            return True
        i += 1
    return False


def strip_generics_and_arrays(text):
    out = []
    i = 0
    while i < len(text):
        step = skip_string_or_none(text, i)
        if step is not None:
            out.append(text[i:step])
            i = step
            continue
        if text[i] == "<":
            end = skip_balanced(text, i, "<", ">")
            out.append(" ")
            i = end
            continue
        if text[i] == "[":
            end = skip_balanced(text, i, "[", "]")
            out.append(" ")
            i = end
            continue
        out.append(text[i])
        i += 1
    return "".join(out)


def skip_balanced(text, i, open_ch, close_ch):
    depth = 0
    while i < len(text):
        step = skip_string_or_none(text, i)
        if step is not None:
            i = step
            continue
        if text[i] == open_ch:
            depth += 1
        elif text[i] == close_ch:
            depth -= 1
            if depth == 0:
                return i + 1
        i += 1
    return i


def warn_duplicates(rows):
    seen = {}
    for row in rows:
        seen.setdefault(row["Key"], []).append(row["File"])
    for key, files in sorted(seen.items()):
        if len(files) > 1:
            print("один ключ в нескольких файлах: {0} ({1})".format(key, ", ".join(files)), file=sys.stderr)


def write_csv(path: Path, rows):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(
            handle,
            fieldnames=["Key", "ShortName", "English", "Source", "Class", "Property", "Namespace", "File"],
            delimiter=";",
            extrasaction="ignore",
        )
        writer.writeheader()
        writer.writerows(rows)


def read_resx(path: Path):
    if path is None or not path.exists():
        print("нет resx: " + str(path), file=sys.stderr)
        return {}
    root = ET.parse(path).getroot()
    values = {}
    for node in root.iter("data"):
        name = node.get("name")
        value = node.find("value")
        if name and value is not None and value.text:
            values[name] = value.text
    return values


def strip_comments(src: str) -> str:
    out = []
    i = 0
    n = len(src)
    while i < n:
        if src.startswith("//", i):
            while i < n and src[i] != "\n":
                i += 1
            continue
        if src.startswith("/*", i):
            i += 2
            while i + 1 < n and not src.startswith("*/", i):
                out.append("\n" if src[i] == "\n" else " ")
                i += 1
            i += 2
            continue
        step = skip_string_or_none(src, i)
        if step is not None:
            out.append(src[i:step])
            i = step
            continue
        out.append(src[i])
        i += 1
    return "".join(out)


def skip_string_or_none(src, i):
    if i >= len(src):
        return None
    if src.startswith('$@"', i) or src.startswith('@"', i) or src.startswith('@$"', i) or src.startswith("$\"", i) or src[i] in "'\"":
        return skip_string(src, i)
    return None


def skip_string(src, i):
    if src.startswith('$@"', i) or src.startswith('@$"', i):
        return skip_interpolated(src, i + 3, verbatim=True)
    if src.startswith('@"', i):
        return skip_verbatim(src, i + 2)
    if src.startswith('$"', i):
        return skip_interpolated(src, i + 2, verbatim=False)
    if src[i] == "'":
        return skip_char(src, i + 1)
    if src[i] == '"':
        return skip_regular(src, i + 1)
    return i + 1


def skip_verbatim(src, i):
    while i < len(src):
        if src.startswith('""', i):
            i += 2
            continue
        if src[i] == '"':
            return i + 1
        i += 1
    return i


def skip_regular(src, i):
    while i < len(src):
        if src[i] == "\\":
            i += 2
            continue
        if src[i] == '"':
            return i + 1
        i += 1
    return i


def skip_char(src, i):
    while i < len(src):
        if src[i] == "\\":
            i += 2
            continue
        if src[i] == "'":
            return i + 1
        i += 1
    return i


def skip_interpolated(src, i, verbatim):
    depth = 1
    while i < len(src) and depth:
        if verbatim and src.startswith('""', i):
            i += 2
            continue
        if not verbatim and src[i] == "\\":
            i += 2
            continue
        if src[i] == "{":
            if i + 1 < len(src) and src[i + 1] == "{":
                i += 2
                continue
            depth += 1
        elif src[i] == "}":
            if i + 1 < len(src) and src[i + 1] == "}":
                i += 2
                continue
            depth -= 1
            i += 1
            continue
        elif src[i] == '"':
            depth -= 1
            i += 1
            continue
        i += 1
    return i


def skip_ws(src, i):
    while i < len(src) and src[i] in " \t\r\n":
        i += 1
    return i


def read_ident(src, i):
    if i >= len(src) or not (src[i].isalpha() or src[i] == "_"):
        return None, i
    j = i + 1
    while j < len(src) and (src[j].isalnum() or src[j] == "_"):
        j += 1
    return src[i:j], j


def read_dotted(src, i):
    name, i = read_ident(src, i)
    if not name:
        return "", i
    parts = [name]
    while True:
        j = skip_ws(src, i)
        if j >= len(src) or src[j] != ".":
            return ".".join(parts), i
        ident, k = read_ident(src, j + 1)
        if not ident:
            return ".".join(parts), i
        parts.append(ident)
        i = k


def run_self_test():
    sample = r'''
namespace Store.Device.Models
{
    public class VerifyIdentifierChange
    {
        [Display(AutoGenerateField = false)]
        public bool ChBoxS { get; set; }

        // [Display(ShortName = "Из комментария")]
        public string Ignored { get; set; }

        [Display(Name = "Строка", Description = "Строка")]
        public override decimal? LineNo { get; set; }

        [Display(Name = "Серийный номер", ShortName = "Серийный номер", Description = "Серийный номер")]
        public override string SerialNumber { get; set; }

        [Display(
            Name = "Имя",
            ShortName = "Имя, полное")]
        public string Title { get; set; }

        [Display(ShortName = "Метод")]
        public void Run() { }

        [Display(ShortName = "Вложенный тип")]
        public class Inner
        {
            [Display(ShortName = "Внутри")]
            public string Name { get; set; }
        }
    }

    public class Box<T>
    {
        [Display(ShortName = "Обобщение")]
        public string Value { get; set; }
    }
}
'''
    rows = scan_source(sample)
    got = {row["Key"]: row["ShortName"] for row in rows}
    expected = {
        "VerifyIdentifierChange_LineNo": "Строка",
        "VerifyIdentifierChange_SerialNumber": "Серийный номер",
        "VerifyIdentifierChange_Title": "Имя, полное",
        "Inner_Name": "Внутри",
        "Box_Value": "Обобщение",
    }
    if got != expected:
        print("self-test: " + repr(got), file=sys.stderr)
        return 1
    print("self-test ok")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
