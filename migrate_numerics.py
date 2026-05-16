#!/usr/bin/env python3
"""
Migrate osuTK Vector2/3/4 to System.Numerics.Vector2/3/4 in C# files.
"""

import os
import re
import sys

# Non-Vector osuTK types that require keeping `using osuTK;`
NON_VECTOR_OSTK_TYPES = re.compile(
    r'\b(Matrix2|Matrix3|Matrix4|Quaternion|MathHelper|Box2|Box2d|Bezier|'
    r'BezierCurve|Half|Vector2d|Vector3d|Vector4d|Vector2h|Vector3h|Vector4h|'
    r'Vector2i|Vector3i|Vector4i)\b'
)

# Directories to process
DIRS = [
    '/home/runner/work/osu-framework/osu-framework/osu.Framework',
    '/home/runner/work/osu-framework/osu-framework/osu.Framework.Tests',
]

def uses_vector_types(content):
    """Check which Vector types (2/3/4) are used in the content."""
    used = set()
    # Avoid matching inside comments/strings is imperfect but good enough
    if re.search(r'\bVector2\b', content):
        used.add('Vector2')
    if re.search(r'\bVector3\b', content):
        used.add('Vector3')
    if re.search(r'\bVector4\b', content):
        used.add('Vector4')
    return used

def uses_non_vector_ostk(content):
    """Check if the file uses non-Vector osuTK types."""
    return bool(NON_VECTOR_OSTK_TYPES.search(content))

def add_numerics_using(content, vector_types_used):
    """
    If file only uses Vector types from osuTK: replace `using osuTK;` with `using System.Numerics;`
    If file also uses non-Vector osuTK types: keep `using osuTK;` and add aliases.
    """
    has_using_ostk = bool(re.search(r'^using osuTK;', content, re.MULTILINE))
    if not has_using_ostk:
        return content

    needs_non_vector = uses_non_vector_ostk(content)

    if not needs_non_vector:
        # Safe to replace `using osuTK;` with `using System.Numerics;`
        # But check if System.Numerics is already there
        if re.search(r'^using System\.Numerics;', content, re.MULTILINE):
            # Just remove `using osuTK;`
            content = re.sub(r'^using osuTK;\n', '', content, flags=re.MULTILINE)
        else:
            content = re.sub(r'^using osuTK;', 'using System.Numerics;', content, flags=re.MULTILINE)
    else:
        # Keep `using osuTK;`, add aliases for Vector types that are used
        # But only if `using System.Numerics;` is not already present as an alias
        aliases_to_add = []
        for vt in sorted(vector_types_used):
            alias = f'using {vt} = System.Numerics.{vt};'
            if alias not in content:
                aliases_to_add.append(alias)

        if aliases_to_add:
            # Insert aliases right after `using osuTK;`
            aliases_str = '\n'.join(aliases_to_add)
            content = re.sub(
                r'^(using osuTK;)',
                r'\1\n' + aliases_str,
                content,
                flags=re.MULTILINE
            )

    return content

def fix_api_differences(content):
    """Fix osuTK-specific Vector2 API calls to System.Numerics equivalents."""

    # Vector2.Dot(ref a, ref b, out float c); → float c = Vector2.Dot(a, b);
    # Pattern: Vector2.Dot(ref <expr>, ref <expr>, out <type> <var>);
    def replace_dot_ref(m):
        a = m.group(1).strip()
        b = m.group(2).strip()
        typ = m.group(3).strip()
        var = m.group(4).strip()
        return f'{typ} {var} = Vector2.Dot({a}, {b});'

    content = re.sub(
        r'Vector2\.Dot\(ref\s+(\w+),\s*ref\s+(\w+),\s*out\s+(float)\s+(\w+)\);',
        replace_dot_ref,
        content
    )

    # Vector2.PerpDot(ref a, ref b, out float c); → float c = Vector2Extensions.PerpDot(a, b);
    def replace_perpdot_ref(m):
        a = m.group(1).strip()
        b = m.group(2).strip()
        typ = m.group(3).strip()
        var = m.group(4).strip()
        return f'{typ} {var} = Vector2Extensions.PerpDot({a}, {b});'

    content = re.sub(
        r'Vector2\.PerpDot\(ref\s+(\w+),\s*ref\s+(\w+),\s*out\s+(float)\s+(\w+)\);',
        replace_perpdot_ref,
        content
    )

    # Vector2.PerpDot(a, b) → Vector2Extensions.PerpDot(a, b)  (non-ref version)
    # Be careful not to replace Vector2Extensions.PerpDot
    content = re.sub(
        r'(?<!Extensions\.)(?<!\w)Vector2\.PerpDot\(',
        'Vector2Extensions.PerpDot(',
        content
    )

    # Vector2.ComponentMin(a, b) → Vector2.Min(a, b)
    content = re.sub(r'\bVector2\.ComponentMin\b', 'Vector2.Min', content)
    content = re.sub(r'\bVector3\.ComponentMin\b', 'Vector3.Min', content)
    content = re.sub(r'\bVector4\.ComponentMin\b', 'Vector4.Min', content)

    # Vector2.ComponentMax(a, b) → Vector2.Max(a, b)
    content = re.sub(r'\bVector2\.ComponentMax\b', 'Vector2.Max', content)
    content = re.sub(r'\bVector3\.ComponentMax\b', 'Vector3.Max', content)
    content = re.sub(r'\bVector4\.ComponentMax\b', 'Vector4.Max', content)

    # .PerpendicularRight (property) → .PerpendicularRight() (extension method)
    # Only replace when not already followed by '('
    content = re.sub(r'\.PerpendicularRight(?!\s*\()', '.PerpendicularRight()', content)

    # .PerpendicularLeft (property) → .PerpendicularLeft() (extension method)
    content = re.sub(r'\.PerpendicularLeft(?!\s*\()', '.PerpendicularLeft()', content)

    # .Xy on osuTK.Vector3 → .Xy.ToSystemNumerics()
    # This is tricky to do automatically; handle specific patterns
    # ExtractScale().Xy → ExtractScale().Xy.ToSystemNumerics()
    content = re.sub(
        r'(ExtractScale\(\))\.Xy\b(?!\.ToSystemNumerics)',
        r'\1.Xy.ToSystemNumerics()',
        content
    )
    # For cases like `scale.Xy` where scale was declared as Vector3
    # We handle these in specific files manually if needed

    return content

def ensure_vector2extensions_using(content):
    """
    If file uses Vector2Extensions.PerpDot (added by our migration) but doesn't 
    have the using for osu.Framework.Graphics, add it.
    """
    if 'Vector2Extensions.' not in content:
        return content
    if 'using osu.Framework.Graphics;' in content:
        return content

    # Check if it's in the osu.Framework.Graphics namespace itself
    if 'namespace osu.Framework.Graphics' in content:
        return content

    # Insert after other using directives
    # Find the last `using` line and insert after
    lines = content.split('\n')
    last_using_idx = -1
    for i, line in enumerate(lines):
        if line.strip().startswith('using ') and not line.strip().startswith('using static'):
            last_using_idx = i

    if last_using_idx >= 0:
        lines.insert(last_using_idx + 1, 'using osu.Framework.Graphics;')
        return '\n'.join(lines)
    return content

def process_file(path):
    """Process a single C# file."""
    with open(path, 'r', encoding='utf-8-sig') as f:
        original = f.read()

    content = original

    # Only process files that have `using osuTK;`
    if not re.search(r'^using osuTK;', content, re.MULTILINE):
        return False

    # Determine what vector types are used
    vector_types = uses_vector_types(content)

    if not vector_types:
        return False  # No Vector types, skip (shouldn't happen in practice)

    # Fix API differences first (before changing using directives)
    content = fix_api_differences(content)

    # Add/replace using directives
    content = add_numerics_using(content, vector_types)

    # Ensure Vector2Extensions namespace is imported if needed
    content = ensure_vector2extensions_using(content)

    if content != original:
        with open(path, 'w', encoding='utf-8') as f:
            f.write(content)
        return True
    return False

def main():
    changed = []
    skipped = []

    for base_dir in DIRS:
        for root, dirs, files in os.walk(base_dir):
            # Skip bin/obj directories
            dirs[:] = [d for d in dirs if d not in ('bin', 'obj', '.git')]
            for fname in files:
                if not fname.endswith('.cs'):
                    continue
                path = os.path.join(root, fname)
                try:
                    if process_file(path):
                        changed.append(path)
                    else:
                        pass
                except Exception as e:
                    print(f'ERROR processing {path}: {e}', file=sys.stderr)

    print(f'Changed {len(changed)} files:')
    for p in changed:
        # Print relative path
        p_rel = p.replace('/home/runner/work/osu-framework/osu-framework/', '')
        print(f'  {p_rel}')

if __name__ == '__main__':
    main()
