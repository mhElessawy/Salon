import asyncio
import os
import sys

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".demo_py")))

import edge_tts


async def main():
    base = os.path.dirname(__file__)
    with open(os.path.join(base, "narration.txt"), "r", encoding="utf-8") as f:
        text = f.read()
    communicate = edge_tts.Communicate(text, "ar-EG-SalmaNeural", rate="+8%")
    await communicate.save(os.path.join(base, "narration.mp3"))


asyncio.get_event_loop().run_until_complete(main())
