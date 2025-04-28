import socket
import threading
import tkinter as tk
from tkinter import scrolledtext, simpledialog

# GUI Setup
root = tk.Tk()
root.title("TCP/UDP Communication GUI")

# Text Area for Logs
log_area = scrolledtext.ScrolledText(root, width=70, height=20, state='disabled')
log_area.grid(row=0, column=0, columnspan=3, padx=10, pady=10)

def log(message):
    log_area['state'] = 'normal'
    log_area.insert(tk.END, message + '\n')
    log_area['state'] = 'disabled'
    log_area.see(tk.END)

# TCP Server
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

# TCP Client
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

# UDP Server
def start_udp_server():
    def udp_server():
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as server_socket:
            server_socket.bind(('localhost', 12346))
            log("[UDP SERVER] Listening...")
            data, addr = server_socket.recvfrom(1024)
            log("[UDP SERVER] Received: " + data.decode())
            server_socket.sendto(b"Hello from UDP server!", addr)
    threading.Thread(target=udp_server, daemon=True).start()

# Persistent UDP Client
udp_client_socket = None

def start_udp_client():
    global udp_client_socket
    udp_client_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

    def receive_udp():
        while True:
            try:
                data, _ = udp_client_socket.recvfrom(1024)
                log("[UDP CLIENT] Received: " + data.decode())
            except:
                break

    threading.Thread(target=receive_udp, daemon=True).start()
    log("[UDP CLIENT] Ready to send messages...")

def send_udp_message():
    global udp_client_socket
    if udp_client_socket:
        msg = message_entry.get()
        if msg:
            udp_client_socket.sendto(msg.encode(), ('localhost', 12346))
            log("[UDP CLIENT] Sent: " + msg)
            message_entry.delete(0, tk.END)

# Buttons
tk.Button(root, text="Start TCP Server", width=20, command=start_tcp_server).grid(row=1, column=0, padx=10, pady=5)
tk.Button(root, text="Start TCP Client", width=20, command=start_tcp_client).grid(row=1, column=1, padx=10, pady=5)
tk.Button(root, text="Start UDP Server", width=20, command=start_udp_server).grid(row=2, column=0, padx=10, pady=5)
tk.Button(root, text="Start UDP Client", width=20, command=start_udp_client).grid(row=2, column=1, padx=10, pady=5)
tk.Button(root, text="Exit", width=20, command=root.quit).grid(row=3, column=1, pady=10)

# UDP Message Entry Field
message_entry = tk.Entry(root, width=50)
message_entry.grid(row=4, column=0, columnspan=2, padx=10, pady=5)

tk.Button(root, text="Send UDP Message", width=20, command=send_udp_message).grid(row=4, column=2, padx=10)

root.mainloop()