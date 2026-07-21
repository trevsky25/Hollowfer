#!/usr/bin/env python3
"""Build the 10 x 8 inch print-production Hollowfen picture book."""

from __future__ import annotations

import hashlib
import html
import re
from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageCms, ImageEnhance, ImageFilter, ImageOps
from pypdf import PdfReader, PdfWriter
from pypdf.generic import (
    ArrayObject,
    DecodedStreamObject,
    DictionaryObject,
    NameObject,
    NumberObject,
    RectangleObject,
    TextStringObject,
)
from reportlab.lib.colors import CMYKColor
from reportlab.lib.enums import TA_CENTER, TA_JUSTIFY, TA_LEFT
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.units import inch
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.pdfgen import canvas
from reportlab.platypus import Paragraph


ROOT = Path(__file__).resolve().parents[1]
STORY_MD = ROOT / "docs" / "story.md"
GENERATED = ROOT / "output" / "picture-book" / "assets" / "generated"
PRINT_ASSETS = ROOT / "output" / "picture-book" / "assets" / "print"
TMP_DIR = ROOT / "tmp" / "pdfs"
FINAL_DIR = ROOT / "output" / "pdf"
RAW_PDF = TMP_DIR / "hollowfen-picture-book-raw.pdf"
FINAL_PDF = FINAL_DIR / "hollowfen-picture-book-print-ready.pdf"

TRIM_W = 10 * inch
TRIM_H = 8 * inch
BLEED = 0.125 * inch
PAGE_W = TRIM_W + (2 * BLEED)
PAGE_H = TRIM_H + (2 * BLEED)
TRIM_BOX = RectangleObject([BLEED, BLEED, PAGE_W - BLEED, PAGE_H - BLEED])
MEDIA_BOX = RectangleObject([0, 0, PAGE_W, PAGE_H])

DPI = 300
FULL_PIXELS = (round(PAGE_W / inch * DPI), round(PAGE_H / inch * DPI))
SCENE_PANEL_H = 2.85 * inch
SCENE_ART_H = PAGE_H - SCENE_PANEL_H
SCENE_PIXELS = (round(PAGE_W / inch * DPI), round(SCENE_ART_H / inch * DPI))

FONT_DIR = Path("/System/Library/Fonts/Supplemental")
FONT_REGULAR = FONT_DIR / "Georgia.ttf"
FONT_BOLD = FONT_DIR / "Georgia Bold.ttf"
FONT_ITALIC = FONT_DIR / "Georgia Italic.ttf"
SRGB_PROFILE = Path("/System/Library/ColorSync/Profiles/sRGB Profile.icc")
CMYK_PROFILE = Path("/Library/ColorSync/Profiles/Recommended/CoatedGRACoL2006.icc")

INK = CMYKColor(0.35, 0.42, 0.45, 0.55)
RUST = CMYKColor(0.05, 0.57, 0.72, 0.25)
GOLD = CMYKColor(0.02, 0.20, 0.65, 0.10)
PAPER = CMYKColor(0.04, 0.07, 0.15, 0.00)
PALE = CMYKColor(0.02, 0.03, 0.07, 0.00)
WHITE = CMYKColor(0, 0, 0, 0)


@dataclass
class Scene:
    act: str
    title: str
    image: Path
    narrative: str


ACT_I_ABRIDGED = {
    "Homecoming": (
        "It had been three years since Wren Tobin walked the east road into Hollowfen. "
        "From the ridge, the valley almost looked as it always had: low roofs in the hollow, "
        "the dark shoulder of the Old Wood, and the pale line of the Wend. Then the road dipped, "
        "and the old picture came apart. The river ran too far south, flooding fields where wheat "
        "should have stood. The millstream was only stones and pale grass. Smoke rose from fewer "
        "chimneys, and cottages near the well were boarded shut. At the Crooked Pintle, Old Bram "
        "swept the same leaves across the threshold. He looked smaller than Wren remembered. "
        "For half a breath he did not know her. Then his face changed. 'Wren?' She stopped beside "
        "the well. 'Evening, Bram.' His eyes went to her pack, her knife, and back to her face. "
        "'Lord help me,' he said softly. 'You've got your father's height.' Behind him, the inn "
        "door stood open on firelight. Beyond it, the mill waited in the failing light. 'I've got "
        "the key inside.'"
    ),
    "The Crooked Pintle": (
        "The common room was smaller than Wren remembered. Half the tables were stacked away, "
        "one window was boarded from within, and the copper pots shone only where hands still "
        "touched them. Bram set his broom against the wall and brought out a grey rag folded around "
        "something small. 'Your da left this with me when he took poorly.' Inside lay an iron key, "
        "black with age and worn smooth by generations of Tobins. Bram held it in both palms before "
        "placing it on the bar. The mill, he said, had not turned since the Wend changed its mind "
        "about where it was going. Wren's father had written of flooded fields, but never of how "
        "quiet the village had become. Bram looked down at his hands. 'He was a good man, your da. "
        "Quiet. Fixed my hinge in spring and never sent a bill.' 'That sounds like him.' Bram's "
        "voice softened. 'I'm sorry, Wren.' It was what everyone would say because there was no "
        "useful thing to say instead. Wren closed her fingers around the key. 'So am I.'"
    ),
    "Your Father's Mill": (
        "The mill key stuck on the first turn. Wren lifted the door by its latch, shouldered it "
        "gently, and tried again. The lock gave with a sound like a cough in an empty church. Inside, "
        "the air was cold and dry with old flour. Tobin's boots stood beneath the bench, toes aligned. "
        "His coat hung nearest the hearth. The kettle waited on the cold stones as if someone had "
        "meant to fill it and lost the thought halfway across the room. Wren moved as carefully as if "
        "her father were asleep upstairs. Ledgers lay tied with cord. Her mother's blue cup still "
        "stood on the mantel, cracked and mended with yellow glue. Through the back window, the "
        "millwheel hung above a dead streambed. When Wren was small, its low wooden turning had spoken "
        "through the walls and made sleep easy. Now there was only wind in dry grass. She removed her "
        "cloak, folded it, then unfolded it because there was no reason to be tidy for a dead man. "
        "'All right, Da,' she said to the room. The room said nothing back."
    ),
    "The Hidden Journal": (
        "A bottom drawer opened only halfway. Wren eased the chest forward until something scraped "
        "inside - paper wrapped in oilcloth and tied with butcher's string. She knew her father's knots. "
        "This one had been tied in hesitation. The journal within was brown leather, softened by rain "
        "and thumb oil. The first pages held recipes in her mother's hand. Then Tobin's careful writing "
        "began: Field Cap. Wood Ear. Pinecrest. Each entry carried a pencil sketch, notes on cap, gills, "
        "stem, and where it grew. One warning had been pressed so hard it tore the paper: never eat what "
        "you cannot name twice. At the back, beneath a mill receipt, Tobin had written to her. The forest "
        "knowledge had belonged to their family. Her grandmother knew. Her mother knew. He had waited to "
        "teach Wren until she was old enough, and waited too long. Wren held the page open until the light "
        "shifted across the floor. Then she closed the book, opened it again, and began at the first entry."
    ),
    "The First Forage": (
        "Morning came pale and cold. Wren packed the journal more carefully than food and walked to the "
        "Edge Woods, where the last garden wall gave up. Villagers spoke of the trees as if they were a "
        "mouth. Wren found only birch shade, wet leaves, pine needles, and the ticking of last night's rain. "
        "Field Caps grew in a ring near the path. She checked the tan caps, wiry stems, and wide pale gills. "
        "Name it twice. Wood Ear clung to a fallen branch like folded leather. Beneath pine duff she found "
        "three brown caps: two wrong, one right. Only then did she cut it. Deeper in the moss, small brown "
        "caps rose on hollow gold stems. Goldfoot, perhaps. Good in broth. False ones nearby. She checked "
        "the forked ridges and left the eager impostors untouched. When she stood, a thin girl with an empty "
        "basket watched from the path. 'You're not afraid of in there.' Wren glanced into the trees. 'I might "
        "be. I'm being polite about it.' The girl considered this. 'Edda,' she said. 'Wren.' 'I know.'"
    ),
    "Marra's Kitchen": (
        "Bram was wiping the same mug when Wren entered with her basket. She folded back the damp cloth. "
        "'Field Caps,' he said. 'Wood Ear. Pinecrest.' Then he saw three gold-stemmed mushrooms tucked in "
        "the corner. 'Marra.' The hard chop of a kitchen knife stopped. Marra came through the door with "
        "rolled sleeves and flour on one arm. She lifted a gold-stem, turned it, smelled it, and set it down "
        "with care. 'Goldfoots.' Bram gripped the bar. 'In this kitchen. After twenty years.' Marra ordered "
        "cold water, then taught Wren how not to bruise them. An hour later the inn smelled like Wren's "
        "mother - not exactly, because nothing was exact after enough years, but close enough to fill the "
        "room. Two men came in and stayed. Then a woman. Then Elder Pell, who claimed he had only come to ask "
        "about a hinge and left with a bowl in both hands. Bram counted three silver and two copper into "
        "Wren's palm. 'First proper bowl we've sold in a month.' The coins were not heavy. They felt heavy."
    ),
    "A Knock at the Door": (
        "Someone knocked on the mill door before sunrise. Sister Almy stood on the threshold with a leather "
        "satchel, a bundle of dried stems tied in red thread, and soil on one sleeve. She was smaller than "
        "Wren remembered and harder to look away from. Her gaze went straight to Tobin's journal. 'You picked "
        "goldfoots yesterday.' 'Field Caps. Wood Ear. Pinecrest.' 'And goldfoots.' Wren said nothing. Almy's "
        "mouth pressed thin. 'You picked the true ones and left the false beside them.' 'I used my father's "
        "journal.' 'Your father couldn't have taught you that. He didn't know it.' The words landed harder "
        "than Wren expected. Almy stepped inside. 'Your grandmother taught me three things,' she said. 'She "
        "forbade me from teaching anyone else.' Wren tightened her hand on the door. She barely remembered her "
        "grandmother. 'No,' said Almy, glancing at Wren's hands. 'But your hands do.' Outside, morning gathered "
        "over the dry millstream. Almy nodded toward the table. 'Sit down, child. We need to talk.'"
    ),
}


def clean_text(text: str) -> str:
    replacements = {
        "\u2010": "-",
        "\u2011": "-",
        "\u2012": "-",
        "\u2013": "-",
        "\u2014": "-",
        "\u2212": "-",
        "\u00a0": " ",
    }
    for source, target in replacements.items():
        text = text.replace(source, target)
    text = re.sub(r"\*\*([^*]+)\*\*", r"\1", text)
    text = re.sub(r"\*([^*]+)\*", r"\1", text)
    text = re.sub(r"\s+", " ", text).strip()
    return text


def parse_story() -> list[Scene]:
    text = STORY_MD.read_text(encoding="utf-8")
    lines = text.splitlines()
    scenes: list[Scene] = []
    act = ""
    title = ""
    image_path: Path | None = None
    narrative = ""
    i = 0

    def commit() -> None:
        nonlocal title, image_path, narrative
        if title and image_path and narrative:
            short_title = re.sub(r"^(Scene \d+|Ending [A-D])\s*-\s*", "", title)
            body = clean_text(narrative)
            if act == "Act I - Arrival" and short_title in ACT_I_ABRIDGED:
                body = clean_text(ACT_I_ABRIDGED[short_title])
            scenes.append(Scene(act=act, title=short_title, image=image_path, narrative=body))
        title = ""
        image_path = None
        narrative = ""

    while i < len(lines):
        line = lines[i]
        if line.startswith("# Act ") and "Completion" not in line:
            commit()
            act = line[2:].strip()
        elif line.startswith("## Scene ") or line.startswith("## Ending "):
            commit()
            title = line[3:].strip()
        elif title and line.startswith("![") and image_path is None:
            match = re.search(r"\(([^)]+)\)", line)
            if match:
                image_path = (STORY_MD.parent / match.group(1)).resolve()
        elif title and line.strip() == "### Narrative Passage":
            i += 1
            passage: list[str] = []
            while i < len(lines) and not lines[i].startswith("### "):
                passage.append(lines[i])
                i += 1
            narrative = "\n".join(passage).strip()
            continue
        i += 1
    commit()

    preferred_remakes = {
        "Your Father's Mill": ROOT / "public/story/cards/fathers-mill-remake.png",
        "Joren's Forge": ROOT / "public/story/cards/jorens-forge-remake.png",
        "Father Calden's Doubt": ROOT / "public/story/cards/caldens-doubt-remake.png",
        "The Chapel Garden Opens": ROOT / "public/story/cards/chapel-garden-remake.png",
    }
    for scene in scenes:
        if scene.title in preferred_remakes and preferred_remakes[scene.title].exists():
            scene.image = preferred_remakes[scene.title]
    return scenes


def register_fonts() -> None:
    for path in (FONT_REGULAR, FONT_BOLD, FONT_ITALIC):
        if not path.exists():
            raise FileNotFoundError(f"Required font not found: {path}")
    pdfmetrics.registerFont(TTFont("HollowfenSerif", str(FONT_REGULAR)))
    pdfmetrics.registerFont(TTFont("HollowfenSerif-Bold", str(FONT_BOLD)))
    pdfmetrics.registerFont(TTFont("HollowfenSerif-Italic", str(FONT_ITALIC)))
    pdfmetrics.registerFontFamily(
        "HollowfenSerif",
        normal="HollowfenSerif",
        bold="HollowfenSerif-Bold",
        italic="HollowfenSerif-Italic",
    )


def print_asset(source: Path, size: tuple[int, int], label: str) -> Path:
    PRINT_ASSETS.mkdir(parents=True, exist_ok=True)
    preset = "cmyk-jpeg-q90-s1-v1"
    digest = hashlib.sha1(
        f"{source.resolve()}:{size}:{source.stat().st_mtime_ns}:{preset}".encode()
    ).hexdigest()[:10]
    output = PRINT_ASSETS / f"{label}-{digest}-{size[0]}x{size[1]}.jpg"
    if output.exists():
        return output

    with Image.open(source) as raw:
        image = ImageOps.exif_transpose(raw).convert("RGB")
        image = ImageOps.fit(image, size, method=Image.Resampling.LANCZOS, centering=(0.5, 0.5))
        image = ImageEnhance.Contrast(image).enhance(1.025)
        image = image.filter(ImageFilter.UnsharpMask(radius=1.2, percent=70, threshold=3))
        try:
            cmyk = ImageCms.profileToProfile(
                image,
                str(SRGB_PROFILE),
                str(CMYK_PROFILE),
                outputMode="CMYK",
                renderingIntent=ImageCms.Intent.PERCEPTUAL,
            )
        except Exception:
            cmyk = image.convert("CMYK")
        cmyk.save(output, "JPEG", quality=90, subsampling=1, dpi=(DPI, DPI), optimize=True)
    return output


def draw_full_art(c: canvas.Canvas, source: Path, label: str) -> None:
    prepared = print_asset(source, FULL_PIXELS, label)
    c.drawImage(str(prepared), 0, 0, width=PAGE_W, height=PAGE_H, mask=None)


def dark_overlay(c: canvas.Canvas, alpha: float = 0.48) -> None:
    c.saveState()
    c.setFillColor(CMYKColor(0.18, 0.16, 0.14, 0.72, alpha=alpha))
    c.rect(0, 0, PAGE_W, PAGE_H, fill=1, stroke=0)
    c.restoreState()


def text_style(name: str, **kwargs) -> ParagraphStyle:
    base = dict(
        fontName="HollowfenSerif",
        fontSize=10,
        leading=12,
        textColor=INK,
        alignment=TA_LEFT,
        spaceAfter=4,
        allowWidows=0,
        allowOrphans=0,
    )
    base.update(kwargs)
    return ParagraphStyle(name, **base)


def balanced_columns(text: str) -> tuple[str, str]:
    sentences = re.split(r"(?<=[.!?])\s+(?=[A-Z'\"])", text)
    target = len(text) / 2
    left: list[str] = []
    right: list[str] = []
    length = 0
    for sentence in sentences:
        if not right and left and length + len(sentence) > target:
            right.append(sentence)
        elif right:
            right.append(sentence)
        else:
            left.append(sentence)
            length += len(sentence) + 1
    if not right and len(left) > 1:
        right.append(left.pop())
    return " ".join(left), " ".join(right)


BODY_STYLE = text_style(
    "body",
    fontSize=9.15,
    leading=10.95,
    alignment=TA_JUSTIFY,
    splitLongWords=False,
)


def draw_scene_page(c: canvas.Canvas, scene: Scene, page_number: int) -> None:
    label = re.sub(r"[^a-z0-9]+", "-", scene.title.lower()).strip("-")
    prepared = print_asset(scene.image, SCENE_PIXELS, label)
    c.drawImage(str(prepared), 0, SCENE_PANEL_H, width=PAGE_W, height=SCENE_ART_H, mask=None)

    c.setFillColor(PAPER)
    c.rect(0, 0, PAGE_W, SCENE_PANEL_H, fill=1, stroke=0)
    c.setFillColor(RUST)
    c.rect(0, SCENE_PANEL_H - 4, PAGE_W, 4, fill=1, stroke=0)

    left = BLEED + 0.42 * inch
    right = PAGE_W - BLEED - 0.42 * inch
    title_y = SCENE_PANEL_H - 0.43 * inch
    c.setFillColor(INK)
    c.setFont("HollowfenSerif-Bold", 17)
    c.drawString(left, title_y, clean_text(scene.title))

    act_short = scene.act.replace(" - ", "  /  ")
    c.setFillColor(RUST)
    c.setFont("HollowfenSerif-Bold", 7.5)
    c.drawRightString(right, title_y + 3, act_short.upper())

    body_y = BLEED + 0.27 * inch
    body_h = title_y - body_y - 0.22 * inch
    gap = 0.32 * inch
    col_w = (right - left - gap) / 2
    columns = balanced_columns(scene.narrative)
    for idx, column_text in enumerate(columns):
        paragraph = Paragraph(html.escape(column_text), BODY_STYLE)
        _, height = paragraph.wrap(col_w, body_h)
        if height > body_h:
            raise RuntimeError(
                f"Text overflow on page {page_number}, column {idx + 1}: {scene.title} "
                f"({height:.1f} > {body_h:.1f})"
            )
        x = left + idx * (col_w + gap)
        paragraph.drawOn(c, x, body_y + body_h - height)


def draw_cover(c: canvas.Canvas) -> None:
    draw_full_art(c, GENERATED / "hollowfen-cover-new.png", "cover")
    c.saveState()
    c.setFillColor(WHITE)
    c.setFont("HollowfenSerif-Bold", 38)
    c.drawString(BLEED + 0.62 * inch, PAGE_H - BLEED - 1.15 * inch, "HOLLOWFEN")
    c.setFont("HollowfenSerif", 21)
    c.drawString(BLEED + 0.65 * inch, PAGE_H - BLEED - 1.60 * inch, "THE FAILING VILLAGE")
    c.setStrokeColor(GOLD)
    c.setLineWidth(1.4)
    c.line(BLEED + 0.65 * inch, PAGE_H - BLEED - 1.82 * inch, BLEED + 3.55 * inch, PAGE_H - BLEED - 1.82 * inch)
    c.setFont("HollowfenSerif-Italic", 11)
    c.drawString(BLEED + 0.65 * inch, PAGE_H - BLEED - 2.12 * inch, "An illustrated story of careful attention and a village returning to life")
    c.restoreState()


def draw_half_title(c: canvas.Canvas) -> None:
    draw_full_art(c, GENERATED / "tobins-journal-frontispiece-new.png", "frontispiece")
    c.saveState()
    c.setFillColor(CMYKColor(0.18, 0.15, 0.12, 0.72, alpha=0.38))
    c.rect(0, PAGE_H * 0.58, PAGE_W * 0.56, PAGE_H * 0.42, fill=1, stroke=0)
    c.setFillColor(WHITE)
    c.setFont("HollowfenSerif-Bold", 28)
    c.drawString(BLEED + 0.55 * inch, PAGE_H - BLEED - 0.95 * inch, "Hollowfen")
    c.setFont("HollowfenSerif-Italic", 12)
    c.drawString(BLEED + 0.57 * inch, PAGE_H - BLEED - 1.32 * inch, "The Failing Village")
    c.restoreState()


def draw_reader_note(c: canvas.Canvas) -> None:
    texture = ROOT / "public" / "ui" / "journal" / "journal-paper.webp"
    draw_full_art(c, texture, "paper-texture")
    c.saveState()
    c.setFillColor(CMYKColor(0, 0, 0, 0, alpha=0.72))
    c.rect(BLEED, BLEED, TRIM_W, TRIM_H, fill=1, stroke=0)
    c.setFillColor(RUST)
    c.setFont("HollowfenSerif-Bold", 9)
    c.drawCentredString(PAGE_W / 2, PAGE_H - BLEED - 1.05 * inch, "A NOTE BEFORE THE ROAD")
    c.setFillColor(INK)
    c.setFont("HollowfenSerif-Bold", 24)
    c.drawCentredString(PAGE_W / 2, PAGE_H - BLEED - 1.55 * inch, "This is Wren Tobin's story.")
    note = (
        "It begins with a river out of place, a silent mill, and knowledge hidden in a drawer. "
        "It does not end with a miracle. Hollowfen changes because people learn to notice what "
        "still grows, share what they know, and decide together what kind of home they mean to keep."
    )
    style = text_style("note", fontSize=13, leading=18, alignment=TA_CENTER)
    p = Paragraph(html.escape(note), style)
    p.wrapOn(c, 6.1 * inch, 3 * inch)
    p.drawOn(c, (PAGE_W - 6.1 * inch) / 2, PAGE_H / 2 - 0.65 * inch)
    c.setFillColor(RUST)
    c.setFont("HollowfenSerif-Italic", 10)
    c.drawCentredString(PAGE_W / 2, BLEED + 0.75 * inch, "Adapted from the complete Hollowfen story and game-design book")
    c.restoreState()


def draw_map(c: canvas.Canvas) -> None:
    c.setFillColor(PAPER)
    c.rect(0, 0, PAGE_W, PAGE_H, fill=1, stroke=0)
    c.setFillColor(RUST)
    c.setFont("HollowfenSerif-Bold", 8)
    c.drawCentredString(PAGE_W / 2, PAGE_H - BLEED - 0.44 * inch, "THE VALLEY OF THE WEND")
    c.setFillColor(INK)
    c.setFont("HollowfenSerif-Bold", 22)
    c.drawCentredString(PAGE_W / 2, PAGE_H - BLEED - 0.82 * inch, "A Map of Hollowfen")
    map_source = ROOT / "public" / "story" / "hollowfen-stylized-map.png"
    map_w = 8.95 * inch
    map_h = 6.15 * inch
    map_pixels = (round(map_w / inch * DPI), round(map_h / inch * DPI))
    prepared = print_asset(map_source, map_pixels, "map")
    x = (PAGE_W - map_w) / 2
    y = BLEED + 0.48 * inch
    c.setStrokeColor(RUST)
    c.setLineWidth(1.2)
    c.rect(x - 5, y - 5, map_w + 10, map_h + 10, fill=0, stroke=1)
    c.drawImage(str(prepared), x, y, width=map_w, height=map_h)


def draw_act_opener(c: canvas.Canvas, art: Path, numeral: str, title: str, epigraph: str, label: str) -> None:
    draw_full_art(c, art, label)
    dark_overlay(c, 0.44)
    c.setFillColor(GOLD)
    c.setFont("HollowfenSerif-Bold", 10)
    c.drawCentredString(PAGE_W / 2, PAGE_H / 2 + 0.70 * inch, f"ACT {numeral}")
    c.setFillColor(WHITE)
    c.setFont("HollowfenSerif-Bold", 31)
    c.drawCentredString(PAGE_W / 2, PAGE_H / 2 + 0.12 * inch, title)
    c.setStrokeColor(GOLD)
    c.setLineWidth(1.2)
    c.line(PAGE_W / 2 - 1.25 * inch, PAGE_H / 2 - 0.16 * inch, PAGE_W / 2 + 1.25 * inch, PAGE_H / 2 - 0.16 * inch)
    c.setFont("HollowfenSerif-Italic", 11.5)
    c.drawCentredString(PAGE_W / 2, PAGE_H / 2 - 0.55 * inch, epigraph)


def draw_choice_page(c: canvas.Canvas) -> None:
    draw_full_art(c, ROOT / "public/story/cards/sealed-letter.png", "four-paths")
    dark_overlay(c, 0.58)
    c.setFillColor(GOLD)
    c.setFont("HollowfenSerif-Bold", 9)
    c.drawCentredString(PAGE_W / 2, PAGE_H - BLEED - 0.72 * inch, "THE MEETING IS OVER. THE CHOICE REMAINS.")
    c.setFillColor(WHITE)
    c.setFont("HollowfenSerif-Bold", 29)
    c.drawCentredString(PAGE_W / 2, PAGE_H - BLEED - 1.35 * inch, "Four Paths for Hollowfen")
    intro = (
        "No single future waits at the end of the road. Each path protects something and risks something. "
        "Read all four endings, then decide which Hollowfen you would choose."
    )
    style = text_style("choice-intro", fontName="HollowfenSerif-Italic", fontSize=11.5, leading=15, textColor=WHITE, alignment=TA_CENTER)
    p = Paragraph(html.escape(intro), style)
    p.wrapOn(c, 6.7 * inch, 1.2 * inch)
    p.drawOn(c, (PAGE_W - 6.7 * inch) / 2, PAGE_H - BLEED - 2.42 * inch)

    choices = [
        ("A", "The Free Hollow", "independence, shared work, local knowledge"),
        ("B", "The Lordly Patronage", "security, protection, and a price in control"),
        ("C", "The Capital", "prosperity through trade, with distance from home"),
        ("D", "The Witch's Path", "a quieter life beside the Old Wood"),
    ]
    start_y = PAGE_H - BLEED - 3.20 * inch
    for idx, (letter, name, cost) in enumerate(choices):
        y = start_y - idx * 0.82 * inch
        c.setFillColor(GOLD)
        c.setFont("HollowfenSerif-Bold", 11)
        c.drawString(BLEED + 1.55 * inch, y, letter)
        c.setFillColor(WHITE)
        c.setFont("HollowfenSerif-Bold", 14)
        c.drawString(BLEED + 2.0 * inch, y, name)
        c.setFont("HollowfenSerif-Italic", 9.2)
        c.drawString(BLEED + 4.30 * inch, y + 1, cost)


def draw_endpaper(c: canvas.Canvas) -> None:
    draw_full_art(c, GENERATED / "hollowfen-restored-endpaper-new.png", "restored-endpaper")
    c.saveState()
    c.setFillColor(CMYKColor(0.18, 0.15, 0.12, 0.74, alpha=0.42))
    c.rect(PAGE_W * 0.54, 0, PAGE_W * 0.46, PAGE_H, fill=1, stroke=0)
    c.setFillColor(WHITE)
    c.setFont("HollowfenSerif-Bold", 21)
    c.drawRightString(PAGE_W - BLEED - 0.58 * inch, PAGE_H - BLEED - 1.08 * inch, "Hollowfen was never")
    c.drawRightString(PAGE_W - BLEED - 0.58 * inch, PAGE_H - BLEED - 1.47 * inch, "saved by a miracle.")
    closing = (
        "It was saved by people who learned to look closely, work carefully, and choose together."
    )
    style = text_style("closing", fontName="HollowfenSerif-Italic", fontSize=12, leading=17, textColor=WHITE, alignment=TA_LEFT)
    p = Paragraph(html.escape(closing), style)
    width = 3.18 * inch
    p.wrapOn(c, width, 2 * inch)
    p.drawOn(c, PAGE_W - BLEED - 0.58 * inch - width, PAGE_H - BLEED - 2.45 * inch)
    c.setFillColor(GOLD)
    c.setFont("HollowfenSerif-Bold", 9)
    c.drawRightString(PAGE_W - BLEED - 0.58 * inch, BLEED + 0.62 * inch, "THE END - AND THE WORK CONTINUES")
    c.restoreState()


def add_output_intent_and_boxes(raw_pdf: Path, final_pdf: Path) -> None:
    reader = PdfReader(str(raw_pdf))
    writer = PdfWriter()
    writer.pdf_header = "%PDF-1.7"
    for page in reader.pages:
        page.mediabox = MEDIA_BOX
        page.cropbox = MEDIA_BOX
        page.bleedbox = MEDIA_BOX
        page.trimbox = TRIM_BOX
        page.artbox = TRIM_BOX
        writer.add_page(page)

    writer.add_metadata(
        {
            "/Title": "Hollowfen - The Failing Village: An Illustrated Story",
            "/Subject": "Print-production picture-book edition with 0.125 inch bleed",
            "/Creator": "Hollowfen picture-book production",
            "/Producer": "ReportLab and pypdf",
        }
    )

    icc_stream = DecodedStreamObject()
    icc_stream.set_data(CMYK_PROFILE.read_bytes())
    icc_stream.update(
        {
            NameObject("/N"): NumberObject(4),
            NameObject("/Alternate"): NameObject("/DeviceCMYK"),
        }
    )
    icc_ref = writer._add_object(icc_stream)
    output_intent = DictionaryObject(
        {
            NameObject("/Type"): NameObject("/OutputIntent"),
            NameObject("/S"): NameObject("/GTS_PDFX"),
            NameObject("/OutputConditionIdentifier"): TextStringObject("Coated GRACoL 2006"),
            NameObject("/Info"): TextStringObject("Coated GRACoL 2006 (ISO 12647-2:2004)"),
            NameObject("/RegistryName"): TextStringObject("http://www.color.org"),
            NameObject("/DestOutputProfile"): icc_ref,
        }
    )
    writer._root_object.update(
        {NameObject("/OutputIntents"): ArrayObject([writer._add_object(output_intent)])}
    )
    with final_pdf.open("wb") as handle:
        writer.write(handle)


def build() -> None:
    TMP_DIR.mkdir(parents=True, exist_ok=True)
    FINAL_DIR.mkdir(parents=True, exist_ok=True)
    register_fonts()
    scenes = parse_story()
    if len(scenes) != 30:
        raise RuntimeError(f"Expected 30 story scenes/endings, found {len(scenes)}")

    by_act: dict[str, list[Scene]] = {}
    for scene in scenes:
        by_act.setdefault(scene.act, []).append(scene)

    c = canvas.Canvas(
        str(RAW_PDF),
        pagesize=(PAGE_W, PAGE_H),
        pageCompression=1,
        initialFontName="HollowfenSerif",
        initialFontSize=10,
        initialLeading=12,
    )
    c.setTitle("Hollowfen - The Failing Village: An Illustrated Story")
    c.setSubject("40-page print-production picture book; 10 x 8 inch trim with 0.125 inch bleed")
    page = 1

    draw_cover(c)
    c.showPage(); page += 1
    draw_half_title(c)
    c.showPage(); page += 1
    draw_reader_note(c)
    c.showPage(); page += 1
    draw_map(c)
    c.showPage(); page += 1

    draw_act_opener(c, ROOT / "public/story/cards/main-menu.png", "I", "Arrival", "A homecoming begins with the river out of place.", "act-1")
    c.showPage(); page += 1
    for scene in by_act["Act I - Arrival"]:
        draw_scene_page(c, scene, page)
        c.showPage(); page += 1

    draw_act_opener(c, GENERATED / "almy-garden-lesson-new.png", "II", "Building", "Careful attention becomes work, and work becomes hope.", "act-2")
    c.showPage(); page += 1
    for scene in by_act["Act II - Building"]:
        draw_scene_page(c, scene, page)
        c.showPage(); page += 1

    draw_act_opener(c, GENERATED / "chapel-garden-lesson-new.png", "III", "Discovery", "The secret in the woods is older than fear.", "act-3")
    c.showPage(); page += 1
    for scene in by_act["Act III - Discovery"]:
        draw_scene_page(c, scene, page)
        c.showPage(); page += 1

    draw_act_opener(c, ROOT / "public/story/cards/wend-source.png", "IV", "The Choice", "What Hollowfen becomes must belong to those who live there.", "act-4")
    c.showPage(); page += 1
    act_four = by_act["Act IV - The Choice"]
    for scene in act_four[:3]:
        draw_scene_page(c, scene, page)
        c.showPage(); page += 1
    draw_choice_page(c)
    c.showPage(); page += 1
    for scene in act_four[3:]:
        draw_scene_page(c, scene, page)
        c.showPage(); page += 1

    draw_endpaper(c)
    c.showPage(); page += 1
    c.save()

    if page - 1 != 40:
        raise RuntimeError(f"Expected a 40-page book, built {page - 1} pages")
    add_output_intent_and_boxes(RAW_PDF, FINAL_PDF)
    print(FINAL_PDF)


if __name__ == "__main__":
    build()
