"""Restore missing glTF joint transforms from inverse bind matrices.

Tripo's v3 export contains non-identity inverse bind matrices, but every joint
node has an identity transform. That makes the skinned mesh collapse in Unity.
This script writes a separate GLB; it never changes the source asset.
"""

import json
import struct
import sys
from pathlib import Path

import numpy as np
from scipy.spatial.transform import Rotation


def read_glb(path: Path):
    raw = path.read_bytes()
    magic, version, length = struct.unpack_from("<4sII", raw)
    if magic != b"glTF" or version != 2 or length != len(raw):
        raise ValueError("Expected a valid glTF 2.0 binary file")
    chunks = []
    offset = 12
    while offset < len(raw):
        size, kind = struct.unpack_from("<I4s", raw, offset)
        offset += 8
        chunks.append((kind, raw[offset : offset + size]))
        offset += size
    return chunks


def main(source: Path, destination: Path, wave_pivots: bool = False):
    chunks = read_glb(source)
    document = json.loads(chunks[0][1])
    binary = bytearray(next(data for kind, data in chunks if kind == b"BIN\0"))
    skin = document["skins"][0]
    accessor = document["accessors"][skin["inverseBindMatrices"]]
    view = document["bufferViews"][accessor["bufferView"]]
    if accessor["type"] != "MAT4" or accessor["componentType"] != 5126:
        raise ValueError("Expected float32 inverse bind matrices")
    offset = view.get("byteOffset", 0) + accessor.get("byteOffset", 0)
    joints = skin["joints"]
    if accessor["count"] != len(joints):
        raise ValueError("Joint count and inverse bind matrix count differ")

    parent_of = {}
    for parent, node in enumerate(document["nodes"]):
        for child in node.get("children", []):
            parent_of[child] = parent

    world = {}
    for index, joint in enumerate(joints):
        matrix = np.array(struct.unpack_from("<16f", binary, offset + 64 * index))
        inverse_bind = matrix.reshape(4, 4).T
        world[joint] = np.linalg.inv(inverse_bind)

    if wave_pivots:
        # The exported skeleton lies on a different axis than the visible mesh.
        # Move only the waving arm's pivots into its weighted vertex regions.
        # Replacing each inverse bind matrix keeps the untouched rest mesh fixed.
        arm_pivots = {
            "mixamorig:LeftArm": (0.11, 0.48, 0.02),
            "mixamorig:LeftForeArm": (0.225, 0.39, 0.025),
            "mixamorig:LeftHand": (0.31, 0.35, 0.04),
        }
        for name, position in arm_pivots.items():
            joint = next(index for index in joints if document["nodes"][index].get("name") == name)
            bind = np.eye(4)
            bind[:3, 3] = position
            world[joint] = bind
            joint_index = joints.index(joint)
            matrix = np.linalg.inv(bind).T.astype("<f4").reshape(16)
            struct.pack_into("<16f", binary, offset + 64 * joint_index, *matrix)

    joint_set = set(joints)
    for joint in joints:
        node = document["nodes"][joint]
        if any(key in node for key in ("matrix", "translation", "rotation", "scale")):
            raise ValueError(f"Joint {joint} already has a local transform")
        parent = parent_of.get(joint)
        if parent is not None and parent not in joint_set and parent != skin.get("skeleton", 1):
            raise ValueError(f"Unexpected non-joint parent for joint {joint}")
        parent_world = world[parent] if parent in joint_set else np.eye(4)
        local = np.linalg.inv(parent_world) @ world[joint]
        node["translation"] = [float(value) for value in local[:3, 3]]
        node["rotation"] = [float(value) for value in Rotation.from_matrix(local[:3, :3]).as_quat()]

    encoded = json.dumps(document, separators=(",", ":"), ensure_ascii=False).encode("utf-8")
    encoded += b" " * ((-len(encoded)) % 4)
    output = bytearray(struct.pack("<4sII", b"glTF", 2, 0))
    output += struct.pack("<I4s", len(encoded), b"JSON") + encoded
    for kind, data in chunks[1:]:
        if kind == b"BIN\0":
            data = binary
        output += struct.pack("<I4s", len(data), kind) + data
    struct.pack_into("<I", output, 8, len(output))
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_bytes(output)
    print(f"Restored {len(joints)} joint transforms; wave pivots={wave_pivots}: {destination}")


if __name__ == "__main__":
    main(Path(sys.argv[1]), Path(sys.argv[2]), "--wave-pivots" in sys.argv[3:])
