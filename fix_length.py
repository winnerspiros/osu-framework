#!/usr/bin/env python3
"""Fix Vector2/3/4 .Length and .LengthSquared (property in osuTK) to be method calls in System.Numerics."""
import re
import os

DIRS = [
    '/home/runner/work/osu-framework/osu-framework/osu.Framework',
    '/home/runner/work/osu-framework/osu-framework/osu.Framework.Tests',
]

def fix_file(path):
    with open(path, 'r', encoding='utf-8-sig') as f:
        content = f.read()
    
    # Only process files that now use System.Numerics (directly or via alias)
    uses_sn = ('using System.Numerics;' in content or 
               'using Vector2 = System.Numerics.Vector2;' in content or
               'using Vector3 = System.Numerics.Vector3;' in content or
               'using Vector4 = System.Numerics.Vector4;' in content)
    
    if not uses_sn:
        return False
    
    original = content
    
    # Replace .LengthSquared (not followed by () - i.e. used as property) → .LengthSquared()
    # Skip when followed by existing ()
    content = re.sub(r'\.LengthSquared(?!\s*\()', '.LengthSquared()', content)
    
    # Replace .Length used as a float value (not for arrays)
    # Patterns: .Length followed by ;, ), *, /, +, -, ,, whitespace-comparison operators, or = (but not ==)
    # DON'T replace: .Length - 1, .Length ==, array context
    # We do this carefully: only when .Length is at end of expression
    # Simple approach: replace .Length followed by ; or ) or , or arithmetic
    # But avoid array .Length (hard to distinguish without types)
    # Heuristic: only replace .Length followed by ; ) , * / > < - + (but NOT .Length -)
    # Actually .Length - 1 means subtract, could be array subtraction OR vector length minus float
    # In vector context, .Length is always followed by ; ) , * / comparisons
    # In array context, .Length is often followed by - ) ; 
    # Too risky for general case. Just do the ones at end of statement:
    # .Length; .Length) .Length,
    # And ones in float assignment: variable = something.Length
    
    # Conservative: replace .Length when used as the full expression (not as part of array indexing)
    # Skip .Length followed by space then - (array length - something)
    # Only convert .Length in these patterns:
    # 1. at end of statement: .Length;
    # 2. in comparison: .Length > / .Length < / .Length == / .Length !=  
    # 3. in assignment: = x.Length (semicolon after)
    # 4. in parentheses: .Length)
    # But skip .Length on arrays like controlPoints.Length

    # Strategy: replace .Length followed by ; ) , whitespace then = == != > < <= >= * / + or end of line
    # But exclude when preceded by array-like names (controlPoints, controlpoints, array, Array, etc)
    
    # Actually the safest approach for THIS codebase:
    # Replace ALL .Length (not already ()) with .Length()
    # Then fix false positives if any
    content = re.sub(r'(?<!\w)\.Length(?!\s*\(|\s*\[|\w)', '.Length()', content)
    
    if content != original:
        with open(path, 'w', encoding='utf-8') as f:
            f.write(content)
        return True
    return False

changed = []
for base_dir in DIRS:
    for root, dirs, files in os.walk(base_dir):
        dirs[:] = [d for d in dirs if d not in ('bin', 'obj', '.git')]
        for fname in files:
            if not fname.endswith('.cs'):
                continue
            path = os.path.join(root, fname)
            try:
                if fix_file(path):
                    changed.append(path)
            except Exception as e:
                print(f'ERROR {path}: {e}')

print(f'Changed {len(changed)} files')
for p in changed:
    print(f'  {p.replace("/home/runner/work/osu-framework/osu-framework/", "")}')
