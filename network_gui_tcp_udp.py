import socket
import threading
import tkinter as tk
from tkinter import scrolledtext, simpledialog

# GUI Setup
root = tk.Tk()
root.title("TCP/UDP Communication GUI")

# Text Area for Logs
log_area = scrolledtext.ScrolledText(root, width=60, height=20, state='disabled')
log_area.grid(row=0, column=0, columnspan=3, padx=10, pady=10)

# Log printing
def log(message):
    log_area['state'] = 'normal'
    log_area.insert(tk.END, message + '\n')
    log_area['state'] = 'disabled'
    log_area.see(tk.END)

# Server functions
def start_tcp_server():
    def tcp_server():
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as server_socket:
            server_socket.bind(('localhost', 12345))
            server_socket.listen(1)
            log("[TCP SERVER] Waiting for a connection...")
            conn, addr = server_socket.accept()
            with conn:
                log(f"[TCP SERVER] Connected to {addr}")
                data = conn.recv(1024)
                log("[TCP SERVER] Received: " + data.decode())
                conn.send(b"Hello from TCP server!")
    threading.Thread(target=tcp_server, daemon=True).start()

def start_udp_server():
    def udp_server():
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as server_socket:
            server_socket.bind(('localhost', 12346))
            log("[UDP SERVER] Listening...")
            data, addr = server_socket.recvfrom(1024)
            log("[UDP SERVER] Received: " + data.decode())
            server_socket.sendto(b"Hello from UDP server!", addr)
    threading.Thread(target=udp_server, daemon=True).start()

# Client functions
def start_tcp_client():
    message = simpledialog.askstring("Input", "Enter message to send:")
    if not message: return
    def tcp_client():
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as client_socket:
            client_socket.connect(('localhost', 12345))
            client_socket.send(message.encode())
            data = client_socket.recv(1024)
            log("[TCP CLIENT] Received: " + data.decode())
    threading.Thread(target=tcp_client, daemon=True).start()

def start_udp_client():
    message = simpledialog.askstring("Input", "Enter message to send:")
    if not message: return
    def udp_client():
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as client_socket:
            client_socket.sendto(message.encode(), ('localhost', 12346))
            data, _ = client_socket.recvfrom(1024)
            log("[UDP CLIENT] Received: " + data.decode())
    threading.Thread(target=udp_client, daemon=True).start()

# Buttons
tk.Button(root, text="Start TCP Server", width=20, command=start_tcp_server).grid(row=1, column=0, padx=10, pady=5)
tk.Button(root, text="Start TCP Client", width=20, command=start_tcp_client).grid(row=1, column=1, padx=10, pady=5)
tk.Button(root, text="Start UDP Server", width=20, command=start_udp_server).grid(row=2, column=0, padx=10, pady=5)
tk.Button(root, text="Start UDP Client", width=20, command=start_udp_client).grid(row=2, column=1, padx=10, pady=5)
tk.Button(root, text="Exit", width=20, command=root.quit).grid(row=3, column=1, pady=10)

root.mainloop()