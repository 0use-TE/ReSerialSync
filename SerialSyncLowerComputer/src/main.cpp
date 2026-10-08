#include <Arduino.h>
#include <cstring>

namespace {
constexpr unsigned long BaudRate = 115200;
constexpr size_t MaxFrameBytes = 256;
// Demonstration fallback for sending without a line ending. Real protocols
// should define explicit framing instead of relying on an idle timeout.
constexpr unsigned long IdleTimeoutMs = 50;

uint8_t frame[MaxFrameBytes];
size_t frameLength = 0;
bool frameOverflow = false;
unsigned long lastReceivedAt = 0;
unsigned long receivedFrames = 0;
unsigned long transmittedFrames = 0;

void reply(const char* text) {
  Serial.println(text);
  ++transmittedFrames;
}

bool isCommand(const char* command) {
  const size_t length = std::strlen(command);
  return frameLength == length && std::memcmp(frame, command, length) == 0;
}

void finishFrame() {
  if (frameOverflow) {
    ++receivedFrames;
    reply("ERR frame too long (maximum 256 bytes)");
  } else if (frameLength != 0) {
    ++receivedFrames;
    if (isCommand("PING")) {
      reply("PONG");
    } else if (isCommand("INFO")) {
      reply("{\"board\":\"ESP32\",\"baud\":115200,\"protocol\":\"text-v1\"}");
    } else if (isCommand("STATUS")) {
      ++transmittedFrames;
      Serial.printf("{\"uptime_ms\":%lu,\"rx_frames\":%lu,\"tx_frames\":%lu}\r\n",
                    static_cast<unsigned long>(millis()), receivedFrames, transmittedFrames);
    } else if (isCommand("HELP")) {
      reply("PING | INFO | STATUS | HELP | ECHO <text>; other messages receive ACK <message>");
    } else {
      const bool echo = frameLength >= 5 && std::memcmp(frame, "ECHO ", 5) == 0;
      const size_t offset = echo ? 5 : 0;
      if (!echo) Serial.print("ACK ");
      // Explicit length preserves UTF-8 and embedded zero bytes without using String.
      Serial.write(frame + offset, frameLength - offset);
      Serial.println();
      ++transmittedFrames;
    }
  }

  frameLength = 0;
  frameOverflow = false;
}

void receiveByte(uint8_t value) {
  lastReceivedAt = millis();
  if (value == '\r' || value == '\n') {
    finishFrame(); // Empty frames are ignored, so CRLF only gets one reply.
    return;
  }

  if (frameOverflow) return;
  if (frameLength == MaxFrameBytes) {
    frameOverflow = true; // Discard the rest of this frame until its boundary.
    return;
  }
  frame[frameLength++] = value;
}
} // namespace

void setup() {
  Serial.begin(BaudRate, SERIAL_8N1);
  reply("READY SerialSyncLowerComputer; send HELP for commands");
}

void loop() {
  // Process a bounded batch so other work and the ESP32 scheduler get time too.
  for (size_t processed = 0; processed < 512 && Serial.available() > 0; ++processed) {
    const int value = Serial.read();
    if (value >= 0) receiveByte(static_cast<uint8_t>(value));
  }
  if (Serial.available() == 0 && (frameLength != 0 || frameOverflow) &&
      static_cast<unsigned long>(millis() - lastReceivedAt) >= IdleTimeoutMs) {
    finishFrame();
  }
  delay(1);
}
