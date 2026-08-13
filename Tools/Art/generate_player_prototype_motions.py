"""Generate the temporary protagonist pixel-art atlas used by Unity.

The visual design is a deliberately simplified 64 px interpretation of the
image-generated protagonist reference in Assets/Art/Player/Prototype.
"""

from __future__ import annotations

from dataclasses import dataclass, replace
from math import cos, radians, sin
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


CELL = 64
COLS = 12
# Grounded body pixels end at top-origin y=57. The boundary immediately below
# the feet is y=58, leaving six transparent rows for effects and a stable pivot.
FOOT_BASE_Y = 56
GROUND_LINE_Y = 58
BOTTOM_PADDING = CELL - GROUND_LINE_Y

ROOT = Path(__file__).resolve().parents[2]
OUTPUT_DIR = ROOT / "Assets" / "Art" / "Player" / "Prototype"
ATLAS_PATH = OUTPUT_DIR / "PlayerPrototype_Motions.png"
PREVIEW_PATH = OUTPUT_DIR / "PlayerPrototype_MotionPreview.png"


OUTLINE = (8, 13, 18, 255)
SHADOW = (30, 38, 45, 255)
CLOAK = (43, 53, 62, 255)
CLOAK_LIGHT = (62, 75, 84, 255)
METAL_DARK = (64, 75, 81, 255)
METAL = (124, 136, 139, 255)
METAL_LIGHT = (187, 194, 190, 255)
CYAN_DARK = (0, 110, 145, 255)
CYAN = (0, 218, 255, 255)
CYAN_LIGHT = (198, 252, 255, 255)
RED = (255, 70, 68, 255)
HEAL = (80, 255, 170, 255)


@dataclass(frozen=True)
class Pose:
    dx: int = 0
    dy: int = 0
    lean: int = 0
    crouch: int = 0
    front_foot: int = 6
    back_foot: int = -5
    front_knee: int = 2
    back_knee: int = -2
    hand_x: int = 8
    hand_y: int = 9
    offhand_x: int = 4
    offhand_y: int = 7
    sword_angle: float = 28.0
    sword_length: int = 23
    cape: int = 0
    trail: tuple[int, int] | None = None
    glow: int = 0
    spark: bool = False
    hit: bool = False
    heal: int = 0
    rotation: int = 0
    alpha: int = 255
    prone: int = 0
    rest: int = 0
    landing: int = 0


@dataclass(frozen=True)
class Motion:
    name: str
    frames: tuple[Pose, ...]
    loop: bool
    times: tuple[float, ...]


def _line(draw: ImageDraw.ImageDraw, points, fill, width=3, outline_width=5):
    draw.line(points, fill=OUTLINE, width=outline_width, joint="curve")
    draw.line(points, fill=fill, width=width, joint="curve")


def _joint(draw: ImageDraw.ImageDraw, point, fill=METAL_DARK, radius=2):
    x, y = point
    draw.ellipse((x - radius, y - radius, x + radius, y + radius), fill=OUTLINE)
    if radius > 1:
        draw.point((x, y), fill=fill)


def _arc(draw: ImageDraw.ImageDraw, center, radius, start, end, color, width):
    x, y = center
    box = (x - radius, y - radius, x + radius, y + radius)
    draw.arc(box, start=start, end=end, fill=color, width=width)


def _draw_energy_particles(draw: ImageDraw.ImageDraw, cx: int, cy: int, strength: int, color=CYAN):
    offsets = [(-12, -7), (-9, 10), (10, -10), (13, 5), (-4, -15), (5, 14)]
    for index, (ox, oy) in enumerate(offsets[: max(1, min(len(offsets), strength + 1))]):
        size = 1 + (index + strength) % 2
        draw.rectangle((cx + ox, cy + oy, cx + ox + size, cy + oy + size), fill=color)


def _draw_stick_prone(layer: Image.Image, pose: Pose):
    draw = ImageDraw.Draw(layer)
    y = 52 + min(2, pose.prone)
    head = (46, y - 4)
    draw.ellipse((head[0] - 5, head[1] - 5, head[0] + 5, head[1] + 5), fill=OUTLINE)
    draw.ellipse((head[0] - 3, head[1] - 3, head[0] + 3, head[1] + 3), fill=METAL_LIGHT)
    draw.point((head[0] + 3, head[1] - 1), fill=CYAN)
    _line(draw, [(41, y - 2), (29, y - 2), (18, y)], METAL, width=3, outline_width=5)
    _line(draw, [(30, y - 2), (23, y - 7), (16, y - 6)], METAL_DARK, width=2, outline_width=4)
    _line(draw, [(20, y), (12, y + 2), (7, y + 2)], METAL, width=2, outline_width=4)
    _line(draw, [(20, y), (14, y - 3), (9, y - 2)], METAL_DARK, width=2, outline_width=4)
    _line(draw, [(32, y - 1), (49, y + 2), (61, y + 2)], CYAN, width=2, outline_width=4)
    draw.line([(50, y + 2), (61, y + 2)], fill=CYAN_LIGHT, width=1)


def _draw_stick_rest(layer: Image.Image, pose: Pose):
    draw = ImageDraw.Draw(layer)
    bob = -1 if pose.rest == 2 else 0
    head = (29, 31 + bob)
    shoulder = (30, 37 + bob)
    hip = (31, 45)
    draw.ellipse((head[0] - 6, head[1] - 6, head[0] + 6, head[1] + 6), fill=OUTLINE)
    draw.ellipse((head[0] - 4, head[1] - 4, head[0] + 4, head[1] + 4), fill=METAL_LIGHT)
    draw.point((head[0] + 4, head[1]), fill=CYAN)
    _line(draw, [shoulder, hip], METAL, width=3, outline_width=5)
    _line(draw, [shoulder, (37, 41), (44, 46)], METAL, width=2, outline_width=4)
    _line(draw, [shoulder, (27, 42), (34, 46)], METAL_DARK, width=2, outline_width=4)
    _line(draw, [hip, (23, 51), (30, 56)], METAL_DARK, width=3, outline_width=5)
    _line(draw, [hip, (40, 52), (50, 56)], METAL, width=3, outline_width=5)
    _line(draw, [(44, 46), (49, 54), (59, 54)], CYAN, width=2, outline_width=4)
    if pose.glow:
        _draw_energy_particles(draw, 33, 41, pose.glow)


def _draw_stick_character(layer: Image.Image, pose: Pose):
    if pose.prone:
        _draw_stick_prone(layer, pose)
        return
    if pose.rest:
        _draw_stick_rest(layer, pose)
        return

    draw = ImageDraw.Draw(layer)
    root_x = 30 + pose.dx
    base_y = FOOT_BASE_Y + pose.dy
    hip = (root_x, 42 + pose.dy + pose.crouch)
    shoulder = (root_x + pose.lean, 33 + pose.dy + pose.crouch // 2)
    head = (shoulder[0] + 1, shoulder[1] - 8)

    # Head diameter is 12 px and standing height is 37 px: approximately 3 heads tall.
    draw.ellipse((head[0] - 6, head[1] - 6, head[0] + 6, head[1] + 6), fill=OUTLINE)
    draw.ellipse((head[0] - 4, head[1] - 4, head[0] + 4, head[1] + 4), fill=METAL_LIGHT)
    draw.point((head[0] + 4, head[1]), fill=CYAN)

    back_knee = (hip[0] + pose.back_knee, 49 + pose.dy + max(0, pose.crouch // 2))
    back_foot = (root_x + pose.back_foot, base_y)
    _line(draw, [hip, back_knee, back_foot], METAL_DARK, width=2, outline_width=4)
    _joint(draw, back_knee, METAL_DARK, radius=2)

    # One thick spine keeps the torso readable while retaining stick-figure simplicity.
    _line(draw, [shoulder, hip], METAL, width=3, outline_width=5)
    draw.line((shoulder[0] - 3, shoulder[1], shoulder[0] + 4, shoulder[1]), fill=METAL_LIGHT, width=2)

    offhand = (shoulder[0] + pose.offhand_x, shoulder[1] + pose.offhand_y)
    off_elbow = ((shoulder[0] + offhand[0]) // 2 - 1, (shoulder[1] + offhand[1]) // 2 + 1)
    _line(draw, [shoulder, off_elbow, offhand], METAL_DARK, width=2, outline_width=4)
    _joint(draw, off_elbow, METAL_DARK, radius=2)

    front_knee = (hip[0] + pose.front_knee, 49 + pose.dy + max(0, pose.crouch // 2))
    front_foot = (root_x + pose.front_foot, base_y)
    _line(draw, [hip, front_knee, front_foot], METAL, width=2, outline_width=4)
    _joint(draw, front_knee, METAL, radius=2)
    draw.line((front_foot[0] - 2, base_y, front_foot[0] + 3, base_y), fill=OUTLINE, width=2)
    draw.line((back_foot[0] - 2, base_y, back_foot[0] + 3, base_y), fill=OUTLINE, width=2)

    hand = (shoulder[0] + pose.hand_x, shoulder[1] + pose.hand_y)
    elbow = ((shoulder[0] + hand[0]) // 2 + 2, (shoulder[1] + hand[1]) // 2)
    _line(draw, [shoulder, elbow, hand], METAL, width=2, outline_width=4)
    _joint(draw, elbow, METAL, radius=2)
    _joint(draw, hand, METAL_LIGHT, radius=2)

    angle = radians(pose.sword_angle)
    blade_start = (hand[0] + int(round(cos(angle) * 4)), hand[1] + int(round(sin(angle) * 4)))
    blade_end = (
        hand[0] + int(round(cos(angle) * pose.sword_length)),
        hand[1] + int(round(sin(angle) * pose.sword_length)),
    )
    guard_dx = int(round(cos(angle + 1.5708) * 3))
    guard_dy = int(round(sin(angle + 1.5708) * 3))
    draw.line(
        [(hand[0] - guard_dx, hand[1] - guard_dy), (hand[0] + guard_dx, hand[1] + guard_dy)],
        fill=OUTLINE,
        width=2,
    )
    draw.line([hand, blade_end], fill=OUTLINE, width=4)
    draw.line([blade_start, blade_end], fill=CYAN, width=2)
    draw.line([blade_start, blade_end], fill=CYAN_LIGHT, width=1)

    if pose.trail is not None:
        start, end = pose.trail
        _arc(draw, hand, 23, start, end, (0, 218, 255, 170), 4)
        _arc(draw, hand, 20, start, end, (198, 252, 255, 210), 1)
    if pose.glow:
        _draw_energy_particles(draw, hand[0], hand[1], pose.glow)
        if pose.glow >= 3:
            _arc(draw, hand, 9 + pose.glow, 0, 300, CYAN, 2)
    if pose.spark:
        sx, sy = blade_end
        draw.line((sx - 6, sy, sx + 6, sy), fill=CYAN_LIGHT, width=2)
        draw.line((sx, sy - 6, sx, sy + 6), fill=CYAN_LIGHT, width=2)
        draw.line((sx - 4, sy - 4, sx + 4, sy + 4), fill=CYAN, width=1)
        draw.line((sx + 4, sy - 4, sx - 4, sy + 4), fill=CYAN, width=1)
    if pose.hit:
        hx, hy = shoulder[0] + 7, shoulder[1] + 1
        draw.line((hx - 7, hy, hx + 5, hy), fill=RED, width=2)
        draw.line((hx, hy - 6, hx, hy + 6), fill=RED, width=2)
        draw.line((hx - 4, hy - 4, hx + 4, hy + 4), fill=(255, 188, 100, 255), width=1)
    if pose.heal:
        orb_y = shoulder[1] - 2 - pose.heal
        draw.ellipse((shoulder[0] - 3, orb_y - 3, shoulder[0] + 3, orb_y + 3), fill=(20, 90, 70, 220))
        draw.rectangle((shoulder[0] - 1, orb_y - 1, shoulder[0] + 1, orb_y + 1), fill=HEAL)
        _draw_energy_particles(draw, shoulder[0], orb_y, min(5, pose.heal + 1), HEAL)
    if pose.landing:
        radius = 7 + pose.landing * 3
        _arc(draw, (root_x, base_y), radius, 190, 350, CYAN, 2)
        draw.line((root_x - radius, base_y, root_x + radius, base_y), fill=CYAN_DARK, width=1)


def _draw_prone(layer: Image.Image, pose: Pose):
    draw = ImageDraw.Draw(layer)
    shift = pose.prone
    y = 51 + min(3, shift)
    draw.polygon([(10, y - 3), (36, y - 8), (50, y - 3), (46, y + 3), (17, y + 3)], fill=OUTLINE)
    draw.polygon([(14, y - 2), (35, y - 6), (47, y - 2), (43, y + 1), (18, y + 1)], fill=CLOAK)
    draw.ellipse((38, y - 9, 49, y + 1), fill=OUTLINE)
    draw.rectangle((40, y - 7, 47, y - 1), fill=METAL)
    draw.point((47, y - 4), fill=CYAN)
    _line(draw, [(32, y - 2), (50, y + 1), (61, y + 1)], CYAN, width=2, outline_width=4)
    draw.line([(51, y + 1), (61, y + 1)], fill=CYAN_LIGHT, width=1)


def _draw_rest(layer: Image.Image, pose: Pose):
    draw = ImageDraw.Draw(layer)
    bob = -1 if pose.rest == 2 else 0
    head = (32, 27 + bob)
    draw.polygon([(23, 34), (38, 33), (42, 47), (31, 51), (20, 46)], fill=OUTLINE)
    draw.polygon([(25, 35), (36, 35), (39, 45), (31, 48), (23, 44)], fill=CLOAK)
    draw.ellipse((head[0] - 6, head[1] - 6, head[0] + 6, head[1] + 5), fill=OUTLINE)
    draw.rectangle((28, 23 + bob, 37, 30 + bob), fill=METAL)
    draw.rectangle((37, 25 + bob, 39, 29 + bob), fill=METAL_LIGHT)
    draw.point((38, 27 + bob), fill=CYAN_LIGHT)
    _line(draw, [(28, 39), (36, 45), (44, 47)], METAL, width=3, outline_width=5)
    _line(draw, [(29, 47), (21, 55), (31, 56)], METAL_DARK, width=3, outline_width=5)
    _line(draw, [(34, 47), (41, 55), (50, 56)], METAL, width=3, outline_width=5)
    _line(draw, [(44, 47), (48, 55), (58, 55)], CYAN, width=2, outline_width=4)
    if pose.glow:
        _draw_energy_particles(draw, 34, 38, pose.glow)


def _draw_character(layer: Image.Image, pose: Pose):
    if pose.prone:
        _draw_prone(layer, pose)
        return
    if pose.rest:
        _draw_rest(layer, pose)
        return

    draw = ImageDraw.Draw(layer)
    root_x = 30 + pose.dx
    base_y = 56 + pose.dy
    hip = (root_x, 41 + pose.dy + pose.crouch)
    shoulder = (root_x + pose.lean, 29 + pose.dy + pose.crouch // 2)
    head = (shoulder[0] + 1, shoulder[1] - 10)

    # Cape and scarf are drawn first so the silhouette reads while moving.
    cape_tip = max(6, root_x - 17 - pose.cape)
    cape_mid = max(9, root_x - 12 - pose.cape // 2)
    draw.polygon(
        [
            (shoulder[0] - 4, shoulder[1] - 2),
            (shoulder[0] + 2, shoulder[1] + 3),
            (hip[0] - 2, hip[1] + 8),
            (cape_mid, base_y - 4),
            (cape_tip, base_y - 12),
            (cape_mid - 1, shoulder[1] + 7),
        ],
        fill=OUTLINE,
    )
    draw.polygon(
        [
            (shoulder[0] - 3, shoulder[1]),
            (shoulder[0], shoulder[1] + 3),
            (hip[0] - 3, hip[1] + 5),
            (cape_mid, base_y - 6),
            (cape_tip + 3, base_y - 12),
            (cape_mid + 1, shoulder[1] + 8),
        ],
        fill=CLOAK,
    )
    draw.line([(shoulder[0] - 2, shoulder[1] + 3), (cape_mid + 2, base_y - 10)], fill=CLOAK_LIGHT, width=1)

    # Back leg.
    back_knee = (hip[0] + pose.back_knee, 48 + pose.dy + max(0, pose.crouch // 2))
    back_foot = (root_x + pose.back_foot, base_y)
    _line(draw, [hip, back_knee, back_foot], METAL_DARK)
    _joint(draw, back_knee)
    draw.rectangle((back_foot[0] - 3, base_y - 2, back_foot[0] + 3, base_y + 1), fill=OUTLINE)
    draw.line((back_foot[0] - 1, base_y - 1, back_foot[0] + 3, base_y - 1), fill=METAL_DARK, width=1)

    # Torso armor.
    draw.polygon(
        [
            (shoulder[0] - 5, shoulder[1]),
            (shoulder[0] + 5, shoulder[1] + 1),
            (hip[0] + 5, hip[1]),
            (hip[0] - 5, hip[1]),
        ],
        fill=OUTLINE,
    )
    draw.polygon(
        [
            (shoulder[0] - 3, shoulder[1] + 1),
            (shoulder[0] + 4, shoulder[1] + 2),
            (hip[0] + 3, hip[1] - 1),
            (hip[0] - 3, hip[1] - 1),
        ],
        fill=METAL_DARK,
    )
    draw.line([(shoulder[0] + 1, shoulder[1] + 2), (hip[0] + 2, hip[1] - 2)], fill=METAL, width=2)
    draw.rectangle((hip[0] - 5, hip[1] - 1, hip[0] + 5, hip[1] + 2), fill=OUTLINE)
    draw.line((hip[0] - 3, hip[1], hip[0] + 3, hip[1]), fill=METAL_LIGHT, width=1)

    # Head with a forward face plate and one bright eye.
    draw.ellipse((head[0] - 6, head[1] - 6, head[0] + 6, head[1] + 5), fill=OUTLINE)
    draw.rectangle((head[0] - 4, head[1] - 4, head[0] + 4, head[1] + 3), fill=METAL)
    draw.rectangle((head[0] + 3, head[1] - 2, head[0] + 7, head[1] + 3), fill=OUTLINE)
    draw.rectangle((head[0] + 3, head[1] - 1, head[0] + 5, head[1] + 1), fill=METAL_LIGHT)
    draw.point((head[0] + 5, head[1]), fill=CYAN_LIGHT)
    draw.line((head[0] - 3, head[1] - 3, head[0] + 1, head[1] - 5), fill=METAL_LIGHT, width=1)
    draw.rectangle((shoulder[0] - 6, shoulder[1] - 1, shoulder[0] + 5, shoulder[1] + 3), fill=OUTLINE)
    draw.line((shoulder[0] - 5, shoulder[1], shoulder[0] + 3, shoulder[1] + 1), fill=CLOAK_LIGHT, width=2)

    # Off hand braces the body/weapon depending on the pose.
    offhand = (shoulder[0] + pose.offhand_x, shoulder[1] + pose.offhand_y)
    off_elbow = ((shoulder[0] + offhand[0]) // 2 - 1, (shoulder[1] + offhand[1]) // 2 + 1)
    _line(draw, [(shoulder[0] - 2, shoulder[1] + 2), off_elbow, offhand], METAL_DARK)
    _joint(draw, off_elbow)

    # Front leg.
    front_knee = (hip[0] + pose.front_knee, 48 + pose.dy + max(0, pose.crouch // 2))
    front_foot = (root_x + pose.front_foot, base_y)
    _line(draw, [hip, front_knee, front_foot], METAL)
    _joint(draw, front_knee, METAL)
    draw.rectangle((front_foot[0] - 3, base_y - 2, front_foot[0] + 4, base_y + 1), fill=OUTLINE)
    draw.line((front_foot[0] - 1, base_y - 1, front_foot[0] + 4, base_y - 1), fill=METAL_LIGHT, width=1)

    # Sword arm and AI blade.
    hand = (shoulder[0] + pose.hand_x, shoulder[1] + pose.hand_y)
    elbow = ((shoulder[0] + hand[0]) // 2 + 2, (shoulder[1] + hand[1]) // 2)
    _line(draw, [(shoulder[0] + 3, shoulder[1] + 2), elbow, hand], METAL)
    _joint(draw, elbow, METAL)
    _joint(draw, hand, METAL_LIGHT)

    angle = radians(pose.sword_angle)
    blade_start = (hand[0] + int(round(cos(angle) * 4)), hand[1] + int(round(sin(angle) * 4)))
    blade_end = (
        hand[0] + int(round(cos(angle) * pose.sword_length)),
        hand[1] + int(round(sin(angle) * pose.sword_length)),
    )
    guard_dx = int(round(cos(angle + 1.5708) * 4))
    guard_dy = int(round(sin(angle + 1.5708) * 4))
    draw.line(
        [(hand[0] - guard_dx, hand[1] - guard_dy), (hand[0] + guard_dx, hand[1] + guard_dy)],
        fill=OUTLINE,
        width=3,
    )
    draw.line([hand, blade_end], fill=OUTLINE, width=5)
    draw.line([blade_start, blade_end], fill=CYAN_DARK, width=3)
    draw.line([blade_start, blade_end], fill=CYAN, width=2)
    draw.line([blade_start, blade_end], fill=CYAN_LIGHT, width=1)

    if pose.trail is not None:
        start, end = pose.trail
        _arc(draw, hand, 23, start, end, (0, 218, 255, 170), 4)
        _arc(draw, hand, 20, start, end, (198, 252, 255, 210), 1)
    if pose.glow:
        _draw_energy_particles(draw, hand[0], hand[1], pose.glow)
        if pose.glow >= 3:
            _arc(draw, hand, 9 + pose.glow, 0, 300, CYAN, 2)
    if pose.spark:
        sx, sy = blade_end
        draw.line((sx - 6, sy, sx + 6, sy), fill=CYAN_LIGHT, width=2)
        draw.line((sx, sy - 6, sx, sy + 6), fill=CYAN_LIGHT, width=2)
        draw.line((sx - 4, sy - 4, sx + 4, sy + 4), fill=CYAN, width=1)
        draw.line((sx + 4, sy - 4, sx - 4, sy + 4), fill=CYAN, width=1)
    if pose.hit:
        hx, hy = shoulder[0] + 8, shoulder[1] + 2
        draw.line((hx - 7, hy, hx + 5, hy), fill=RED, width=2)
        draw.line((hx, hy - 6, hx, hy + 6), fill=RED, width=2)
        draw.line((hx - 4, hy - 4, hx + 4, hy + 4), fill=(255, 188, 100, 255), width=1)
    if pose.heal:
        orb_y = shoulder[1] - 2 - pose.heal
        draw.ellipse((shoulder[0] - 3, orb_y - 3, shoulder[0] + 3, orb_y + 3), fill=(20, 90, 70, 220))
        draw.rectangle((shoulder[0] - 1, orb_y - 1, shoulder[0] + 1, orb_y + 1), fill=HEAL)
        _draw_energy_particles(draw, shoulder[0], orb_y, min(5, pose.heal + 1), HEAL)
    if pose.landing:
        radius = 7 + pose.landing * 3
        _arc(draw, (root_x, base_y), radius, 190, 350, CYAN, 2)
        draw.line((root_x - radius, base_y, root_x + radius, base_y), fill=CYAN_DARK, width=1)


def render_pose(pose: Pose) -> Image.Image:
    layer = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    _draw_stick_character(layer, pose)
    if pose.rotation:
        layer = layer.rotate(pose.rotation, resample=Image.Resampling.NEAREST, center=(32, 39))
    if pose.alpha < 255:
        alpha = layer.getchannel("A").point(lambda value: value * pose.alpha // 255)
        layer.putalpha(alpha)
    return layer


def times(*values: float) -> tuple[float, ...]:
    return tuple(values)


def build_motions() -> tuple[Motion, ...]:
    idle = tuple(Pose(dy=bob, cape=sway) for bob, sway in [(0, 0), (-1, 1), (0, 0), (1, -1)])
    move = tuple(
        Pose(
            dy=bob,
            front_foot=front,
            back_foot=-front,
            front_knee=front // 2,
            back_knee=-front // 2,
            lean=2,
            cape=3 + abs(front) // 2,
            sword_angle=18,
        )
        for front, bob in [(-8, 0), (-4, 1), (3, 0), (8, 0), (4, 1), (-3, 0)]
    )
    jump = (
        Pose(crouch=4, front_foot=4, back_foot=-3, sword_angle=15),
        Pose(dy=-2, lean=2, front_foot=2, back_foot=-2, front_knee=5, back_knee=-5, cape=4, sword_angle=-5),
        Pose(dy=-5, lean=2, front_foot=3, back_foot=-3, front_knee=1, back_knee=-1, cape=5, sword_angle=0),
        Pose(dy=-4, front_foot=5, back_foot=-4, front_knee=1, back_knee=-1, cape=4, sword_angle=10),
    )
    fall = (
        Pose(dy=-4, front_foot=7, back_foot=-7, front_knee=3, back_knee=-3, hand_y=6, cape=5, sword_angle=5),
        Pose(dy=-2, front_foot=8, back_foot=-6, front_knee=4, back_knee=-3, hand_y=5, cape=4, sword_angle=12),
        Pose(dy=-1, front_foot=7, back_foot=-7, front_knee=3, back_knee=-3, hand_y=6, cape=5, sword_angle=5),
    )
    drop_through = (
        Pose(crouch=3, sword_angle=22),
        Pose(crouch=5, dy=1, front_foot=3, back_foot=-3, sword_angle=10),
        replace(fall[0], dy=-1),
        replace(fall[1], dy=1),
    )
    dash = (
        Pose(crouch=3, lean=2, cape=2, sword_angle=8),
        Pose(dx=1, lean=7, crouch=2, front_foot=9, back_foot=-7, cape=10, sword_angle=0, glow=1),
        Pose(dx=2, lean=9, crouch=2, front_foot=10, back_foot=-8, cape=13, sword_angle=0, glow=2),
        Pose(dx=2, lean=8, crouch=2, front_foot=9, back_foot=-8, cape=12, sword_angle=0, glow=1),
        Pose(dx=1, lean=4, crouch=1, front_foot=7, back_foot=-6, cape=6, sword_angle=10),
    )

    def attack_frames(angles, hit_index, style):
        result = []
        for index, angle in enumerate(angles):
            progress = index / max(1, len(angles) - 1)
            lean = int(round(sin(progress * 3.14159) * (5 if style != "finisher" else 8)))
            trail = None
            glow = 0
            if abs(index - hit_index) <= 1:
                if style == "horizontal":
                    trail = (200, 350)
                elif style == "rising":
                    trail = (225, 330)
                elif style == "thrust":
                    glow = 2
                else:
                    trail = (190, 350)
                    glow = 3
            result.append(
                Pose(
                    lean=lean,
                    crouch=1 if index >= hit_index else 0,
                    front_foot=6 + min(5, lean),
                    back_foot=-5,
                    hand_x=8 + min(4, lean // 2),
                    sword_angle=angle,
                    sword_length=26 if style == "thrust" and abs(index - hit_index) <= 1 else 23,
                    cape=2 + lean,
                    trail=trail,
                    glow=glow,
                )
            )
        return tuple(result)

    attack1 = attack_frames((-115, -80, -35, 0, 25, 42, 32, 28), 3, "horizontal")
    attack2 = attack_frames((48, 35, 12, -20, -58, -95, -35, 28), 3, "rising")
    attack3 = attack_frames((28, 18, 8, 0, 0, 8, 18, 28), 3, "thrust")
    attack4 = attack_frames((28, -55, -90, -115, -125, -92, 8, 35, 58, 45, 28), 6, "finisher")

    parry = (
        Pose(sword_angle=28),
        Pose(lean=1, hand_x=10, hand_y=7, sword_angle=-65, cape=2),
        Pose(lean=1, hand_x=11, hand_y=6, sword_angle=-82, cape=2, glow=1),
        Pose(lean=1, hand_x=11, hand_y=6, sword_angle=-82, cape=1),
        Pose(hand_x=9, hand_y=8, sword_angle=-45),
    )
    parry_success = (
        replace(parry[2], glow=3, spark=True),
        replace(parry[2], glow=5, spark=True, trail=(205, 330)),
        replace(parry[3], glow=2, spark=True),
        replace(parry[4], glow=1),
    )
    parry_fail = (
        replace(parry[2], glow=0),
        Pose(lean=-1, sword_angle=-35, hand_y=9, front_foot=4, back_foot=-7),
        Pose(lean=-4, crouch=2, sword_angle=55, hand_y=10, front_foot=2, back_foot=-8, cape=-1),
        Pose(lean=-2, crouch=1, sword_angle=45, hand_y=10, front_foot=3, back_foot=-7),
    )
    parry_counter = attack_frames((-60, -30, -10, 0, 0, 10, 28), 3, "thrust")
    parry_counter = tuple(replace(pose, glow=max(2, pose.glow), cape=pose.cape + 4) for pose in parry_counter)

    special = (
        Pose(crouch=1, sword_angle=-70, glow=1),
        Pose(crouch=2, sword_angle=-82, glow=2),
        Pose(crouch=3, sword_angle=-90, glow=4),
        Pose(lean=4, crouch=2, sword_angle=-35, glow=5, trail=(180, 350)),
        Pose(lean=7, crouch=2, front_foot=10, back_foot=-8, sword_angle=5, glow=5, trail=(180, 355)),
        Pose(lean=5, crouch=3, front_foot=9, back_foot=-7, sword_angle=45, glow=4, trail=(200, 355), landing=2),
        Pose(lean=2, crouch=4, sword_angle=65, glow=3, landing=3),
        Pose(crouch=2, sword_angle=45, glow=1, landing=1),
        Pose(sword_angle=28),
    )
    hit = (
        Pose(lean=-6, front_foot=2, back_foot=-8, sword_angle=58, cape=-2, hit=True),
        Pose(dx=-2, lean=-8, crouch=1, front_foot=0, back_foot=-9, sword_angle=70, cape=-3, hit=True),
        Pose(dx=-1, lean=-4, crouch=1, front_foot=2, back_foot=-7, sword_angle=50, cape=-1),
    )
    heal = tuple(
        Pose(crouch=1, hand_x=5, hand_y=7, offhand_x=2, offhand_y=8, sword_angle=72, heal=value, glow=1 if value > 2 else 0)
        for value in (0, 1, 2, 4, 3, 1)
    )
    death = (
        replace(hit[0], hit=True),
        Pose(lean=-7, crouch=3, front_foot=1, back_foot=-8, sword_angle=70, cape=-2),
        Pose(lean=-5, crouch=7, front_foot=0, back_foot=-6, sword_angle=80, cape=-2),
        Pose(prone=1),
        Pose(prone=2),
        Pose(prone=3),
        Pose(prone=4),
        Pose(prone=5),
    )
    respawn = tuple(
        Pose(alpha=alpha, glow=glow, dy=dy, sword_angle=28)
        for alpha, glow, dy in [(40, 5, 3), (75, 5, 2), (120, 4, 1), (175, 3, 0), (225, 2, 0), (255, 1, 0)]
    )
    rest = tuple(Pose(rest=stage, glow=1 if stage == 2 else 0) for stage in (1, 2, 1, 1, 2, 1))

    combo_branch = attack_frames((75, 35, -5, -45, -80, -20, 28), 3, "rising")
    combo_branch = tuple(replace(pose, rotation=-10 if index in (2, 3) else 0) for index, pose in enumerate(combo_branch))
    air_recovery = tuple(
        Pose(dy=-2, rotation=angle, front_foot=7, back_foot=-7, sword_angle=5, cape=6, glow=1)
        for angle in (70, 25, -15, -5)
    )
    air_dash_slash = tuple(replace(pose, dy=-3, trail=(205, 350) if index in (2, 3) else pose.trail) for index, pose in enumerate(dash + dash[:1]))
    landing_shock = (
        replace(fall[1], dy=-2),
        Pose(crouch=5, front_foot=8, back_foot=-7, sword_angle=55, landing=1),
        Pose(crouch=6, front_foot=9, back_foot=-8, sword_angle=60, landing=3, glow=3),
        Pose(crouch=4, front_foot=8, back_foot=-7, sword_angle=50, landing=4, glow=2),
        Pose(crouch=2, sword_angle=35, landing=2),
    )

    return (
        Motion("Idle", idle, True, times(0, 0.125, 0.25, 0.375, 0.5)),
        Motion("Move", move, True, times(0, 0.083333, 0.166667, 0.25, 0.333333, 0.416667, 0.5)),
        Motion("Jump", jump, False, times(0, 0.08, 0.18, 0.3, 0.4)),
        Motion("Fall", fall, True, times(0, 0.12, 0.24, 0.36)),
        Motion("DropThrough", drop_through, False, times(0, 0.08, 0.16, 0.26, 0.34)),
        Motion("Dash", dash, False, times(0, 0.033333, 0.066667, 0.116667, 0.166667, 0.183333)),
        Motion("Attack_1", attack1, False, times(0, 0.083333, 0.166667, 0.25, 0.333333, 0.416667, 0.5, 0.6, 0.616667)),
        Motion("Attack_2", attack2, False, times(0, 0.066667, 0.133333, 0.2, 0.266667, 0.333333, 0.4, 0.5, 0.516667)),
        Motion("Attack_3", attack3, False, times(0, 0.083333, 0.166667, 0.25, 0.333333, 0.416667, 0.5, 0.6, 0.616667)),
        Motion("Attack_4", attack4, False, times(0, 0.133333, 0.233333, 0.333333, 0.433333, 0.533333, 0.633333, 0.733333, 0.833333, 0.933333, 1.0, 1.016667)),
        Motion("Parry", parry, False, times(0, 0.05, 0.1, 0.15, 0.2, 0.216667)),
        Motion("ParrySuccess", parry_success, False, times(0, 0.05, 0.1, 0.15, 0.166667)),
        Motion("ParryFail", parry_fail, False, times(0, 0.1, 0.2, 0.3, 0.316667)),
        Motion("ParryCounter", parry_counter, False, times(0, 0.066667, 0.133333, 0.2, 0.3, 0.4, 0.55, 0.566667)),
        Motion("Special", special, False, times(0, 0.08, 0.16, 0.24, 0.32, 0.42, 0.52, 0.65, 0.78, 0.79)),
        Motion("Hit", hit, False, times(0, 0.033333, 0.066667, 0.1)),
        Motion("Heal", heal, False, times(0, 0.12, 0.24, 0.4, 0.58, 0.74, 0.8)),
        Motion("Death", death, False, times(0, 0.1, 0.2, 0.32, 0.45, 0.6, 0.8, 1.0, 1.016667)),
        Motion("Respawn", respawn, False, times(0, 0.12, 0.24, 0.38, 0.55, 0.72, 0.8)),
        Motion("Rest", rest, True, times(0, 0.18, 0.36, 0.54, 0.72, 0.9, 1.08)),
        Motion("ComboBranch", combo_branch, False, times(0, 0.08, 0.16, 0.24, 0.34, 0.46, 0.6, 0.616667)),
        Motion("AirRecovery", air_recovery, False, times(0, 0.08, 0.16, 0.24, 0.32)),
        Motion("AirDashSlash", air_dash_slash, False, times(0, 0.05, 0.1, 0.16, 0.24, 0.34, 0.36)),
        Motion("LandingShock", landing_shock, False, times(0, 0.08, 0.16, 0.24, 0.36, 0.48)),
    )


def build_atlas(motions: tuple[Motion, ...]) -> Image.Image:
    atlas = Image.new("RGBA", (COLS * CELL, len(motions) * CELL), (0, 0, 0, 0))
    for row, motion in enumerate(motions):
        for column, pose in enumerate(motion.frames):
            atlas.alpha_composite(render_pose(pose), (column * CELL, row * CELL))
    return atlas


def checker(size):
    image = Image.new("RGBA", size, (22, 27, 33, 255))
    draw = ImageDraw.Draw(image)
    tile = 8
    for y in range(0, size[1], tile):
        for x in range(0, size[0], tile):
            if (x // tile + y // tile) % 2:
                draw.rectangle((x, y, x + tile - 1, y + tile - 1), fill=(31, 38, 46, 255))
    return image


def build_preview(motions: tuple[Motion, ...], atlas: Image.Image) -> Image.Image:
    label_width = 104
    row_height = CELL
    preview = checker((label_width + COLS * CELL, len(motions) * row_height))
    draw = ImageDraw.Draw(preview)
    font = ImageFont.load_default()
    for row, motion in enumerate(motions):
        y = row * row_height
        draw.rectangle((0, y, label_width - 1, y + row_height - 1), fill=(13, 17, 22, 255))
        draw.text((6, y + 7), motion.name, fill=(225, 236, 242, 255), font=font)
        draw.text((6, y + 24), f"{len(motion.frames)}f {'LOOP' if motion.loop else 'ONCE'}", fill=(0, 218, 255, 255), font=font)
        row_image = atlas.crop((0, y, COLS * CELL, y + CELL))
        preview.alpha_composite(row_image, (label_width, y))
        draw.line((0, y + row_height - 1, preview.width, y + row_height - 1), fill=(55, 66, 76, 255), width=1)
    return preview


def main():
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    motions = build_motions()
    if len(motions) > 32:
        raise ValueError("The atlas row count unexpectedly exceeded the intended prototype scope")
    if any(len(motion.frames) >= len(motion.times) for motion in motions):
        raise ValueError("Each motion must include a terminal time after its final displayed frame")
    atlas = build_atlas(motions)
    atlas.save(ATLAS_PATH, optimize=False)
    preview = build_preview(motions, atlas)
    preview.save(PREVIEW_PATH, optimize=False)
    print(f"Wrote {ATLAS_PATH.relative_to(ROOT)} ({atlas.width}x{atlas.height})")
    print(f"Wrote {PREVIEW_PATH.relative_to(ROOT)} ({preview.width}x{preview.height})")
    print(f"Motions: {len(motions)}, frames: {sum(len(motion.frames) for motion in motions)}")


if __name__ == "__main__":
    main()
