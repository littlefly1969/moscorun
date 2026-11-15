package it.moscorun.probe

import java.io.BufferedInputStream
import java.io.ByteArrayOutputStream
import java.io.IOException
import java.io.InputStream
import java.io.OutputStream
import java.net.InetAddress
import java.net.ServerSocket
import kotlin.concurrent.thread

/**
 * Minimal HTTP/1.1 server on a loopback socket: Android unit tests compile against android.jar,
 * which does not expose com.sun.net.httpserver.
 */
internal class LoopbackServer(private val respond: (requestLine: String, headers: Map<String, String>, body: ByteArray) -> Pair<Int, String>) : AutoCloseable {
    private val socket = ServerSocket(0, 50, InetAddress.getLoopbackAddress())
    val port: Int get() = socket.localPort
    private val worker = thread(isDaemon = true) {
        while (!socket.isClosed) {
            val client = try { socket.accept() } catch (_: IOException) { break }
            client.use { handle(it.getInputStream(), it.getOutputStream()) }
        }
    }

    private fun handle(input: InputStream, output: OutputStream) {
        val reader = BufferedInputStream(input)
        fun line(): String {
            val bytes = ByteArrayOutputStream()
            while (true) {
                val b = reader.read()
                if (b < 0 || b == '\n'.code) break
                if (b != '\r'.code) bytes.write(b)
            }
            return bytes.toString(Charsets.US_ASCII.name())
        }
        val requestLine = line()
        val headers = generateSequence { line().takeIf { it.isNotEmpty() } }
            .associate { it.substringBefore(':').trim().lowercase() to it.substringAfter(':').trim() }
        val body = ByteArrayOutputStream()
        if (headers["transfer-encoding"].equals("chunked", ignoreCase = true)) {
            while (true) {
                val size = line().substringBefore(';').trim().toInt(16)
                if (size == 0) { line(); break }
                body.write(reader.readNBytes(size)); line()
            }
        } else {
            body.write(reader.readNBytes(headers["content-length"]?.toInt() ?: 0))
        }
        val (status, text) = respond(requestLine, headers, body.toByteArray())
        val bytes = text.toByteArray()
        output.write("HTTP/1.1 $status X\r\nContent-Type: application/json\r\nContent-Length: ${bytes.size}\r\nConnection: close\r\n\r\n".toByteArray())
        output.write(bytes)
        output.flush()
    }

    override fun close() { socket.close(); worker.join(2000) }
}
