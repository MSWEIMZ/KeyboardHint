import base64
import sys

# usage: python3 enc-cmd.py "PowerShell command text" -> prints base64 utf-16-le
cmd = sys.argv[1]
print(base64.b64encode(cmd.encode("utf-16-le")).decode())
