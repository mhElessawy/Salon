import { createServer } from "node:http";
import { createWriteStream } from "node:fs";
import { resolve } from "node:path";

const output = resolve("D:/project/Salon/demo-video-assets/salon-demo-arabic.webm");

createServer((req, res) => {
  res.setHeader("Access-Control-Allow-Origin", "http://127.0.0.1:5086");
  res.setHeader("Access-Control-Allow-Methods", "POST, OPTIONS");
  res.setHeader("Access-Control-Allow-Headers", "Content-Type");

  if (req.method === "OPTIONS") {
    res.writeHead(204);
    res.end();
    return;
  }

  if (req.method !== "POST" || req.url !== "/upload") {
    res.writeHead(404);
    res.end("Not found");
    return;
  }

  const file = createWriteStream(output);
  req.pipe(file);
  req.on("end", () => {
    res.writeHead(200, { "Content-Type": "text/plain; charset=utf-8" });
    res.end("saved");
  });
  req.on("error", () => {
    res.writeHead(500);
    res.end("error");
  });
}).listen(5099, "127.0.0.1", () => {
  console.log(`Saving demo video to ${output}`);
});
