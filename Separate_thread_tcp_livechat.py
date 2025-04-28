import socket
import threading
import tkinter as tk
from tkinter import scrolledtext, simpledialog

# Global socket
chat_socket = None
connected = False

# Create GUI
root = tk.Tk()
root.title("Live TCP Chat")

chat_display = scrolledtext.ScrolledText(root, width=60, height=20, state='disabled')
chat_display.grid(row=0, column=0, columnspan=2, padx=10, pady=10)

def log(msg):
    chat_display['state'] = 'normal'
    chat_display.insert(tk.END, msg + '\n')
    chat_display['state'] = 'disabled'
    chat_display.see(tk.END)

def receive_messages():
    global chat_socket
    while connected:
        try:
            message = chat_socket.recv(1024).decode()
            log("Peer: " + message)
        except:
            break

def start_server():
    global chat_socket, connected
    host = simpledialog.askstring("Server", "Enter IP to bind (e.g. 0.0.0.0):", initialvalue="0.0.0.0")
    port = int(simpledialog.askstring("Server", "Enter port:", initialvalue="5555"))
    server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server.bind((host, port))
    server.listen(1)
    log(f"[SERVER] Listening on {host}:{port}...")
    chat_socket, addr = server.accept()
    connected = True
    log(f"[SERVER] Connected to {addr}")
    threading.Thread(target=receive_messages, daemon=True).start()

def connect_to_server():
    global chat_socket, connected
    host = simpledialog.askstring("Client", "Enter server IP:", initialvalue="127.0.0.1")
    port = int(simpledialog.askstring("Client", "Enter server port:", initialvalue="5555"))
    chat_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    chat_socket.connect((host, port))
    connected = True
    log(f"[CLIENT] Connected to {host}:{port}")
    threading.Thread(target=receive_messages, daemon=True).start()

def send_message():
    global chat_socket
    message = message_entry.get()
    if message and connected:
        chat_socket.send(message.encode())
        log("You: " + message)
        message_entry.delete(0, tk.END)

# Buttons and Entry
message_entry = tk.Entry(root, width=45)
message_entry.grid(row=1, column=0, padx=10, pady=5)

tk.Button(root, text="Send", width=15, command=send_message).grid(row=1, column=1, pady=5)
tk.Button(root, text="Start as Server", width=20, command=lambda: threading.Thread(target=start_server, daemon=True).start()).grid(row=2, column=0, pady=5)
tk.Button(root, text="Connect to Server", width=20, command=lambda: threading.Thread(target=connect_to_server, daemon=True).start()).grid(row=2, column=1, pady=5)

root.mainloop()