import socket
import threading
import tkinter as tk
from tkinter import scrolledtext, simpledialog
from datetime import datetime

# Global socket
chat_socket = None
connected = False

# Create GUI
root = tk.Tk()
root.title("Live TCP Chat")

chat_display = scrolledtext.ScrolledText(root, width=60, height=20, state='disabled')
chat_display.grid(row=0, column=0, columnspan=2, padx=10, pady=10)

typing_label = tk.Label(root, text="", fg="grey")
typing_label.grid(row=1, column=0, columnspan=2)

# Global variables for typing status
is_typing = False
typing_timer = None

def log(msg):
    chat_display['state'] = 'normal'
    chat_display.insert(tk.END, msg + '\n')
    chat_display['state'] = 'disabled'
    chat_display.see(tk.END)

def get_timestamp():
    return datetime.now().strftime("%Y-%m-%d %H:%M:%S")

def receive_messages():
    global chat_socket
    while connected:
        try:
            message = chat_socket.recv(1024).decode()
            if message == "<TYPING>":
                typing_label.config(text="User is typing...")
            elif message == "<STOP_TYPING>":
                typing_label.config(text="")
            else:
                log(f"[{get_timestamp()}] Peer: {message}")
                typing_label.config(text="")
        except:
            break

def ask_for_server_details():
    host = simpledialog.askstring("Server", "Enter IP to bind (e.g. 0.0.0.0):", initialvalue="0.0.0.0")
    port = int(simpledialog.askstring("Server", "Enter port:", initialvalue="5555"))
    start_server(host, port)

def start_server(host, port):
    global chat_socket, connected
    server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server.bind((host, port))
    server.listen(1)
    log(f"[SERVER] Listening on {host}:{port}...")
    chat_socket, addr = server.accept()
    connected = True
    log(f"[SERVER] Connected to {addr}")
    threading.Thread(target=receive_messages, daemon=True).start()

def ask_for_client_details():
    host = simpledialog.askstring("Client", "Enter server IP:", initialvalue="127.0.0.1")
    port = int(simpledialog.askstring("Client", "Enter server port:", initialvalue="5555"))
    connect_to_server(host, port)

def connect_to_server(host, port):
    global chat_socket, connected
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
        log(f"[{get_timestamp()}] You: {message}")
        message_entry.delete(0, tk.END)

def send_typing_signal():
    global chat_socket
    if connected:
        chat_socket.send("<TYPING>".encode())

def stop_typing_signal():
    global chat_socket
    if connected:
        chat_socket.send("<STOP_TYPING>".encode())

def on_typing(event):
    global is_typing, typing_timer
    typing_label.config(text="User is typing...")
    if not is_typing:
        # Only send the typing signal if not already sending it
        send_typing_signal()
        is_typing = True
    # Reset the timer for the stop typing event
    if typing_timer:
        root.after_cancel(typing_timer)
    typing_timer = root.after(1000, stop_typing_signal)  # Wait for 1 second to stop typing

def on_stop_typing(event):
    # When key is released, cancel the typing event after a delay
    pass

# Buttons and Entry
message_entry = tk.Entry(root, width=45)
message_entry.grid(row=2, column=0, padx=10, pady=5)

# Bind events to detect typing
message_entry.bind("<KeyPress>", on_typing)

tk.Button(root, text="Send", width=15, command=send_message).grid(row=2, column=1, pady=5)

# Start server button - now uses after() to call ask_for_server_details
tk.Button(root, text="Start as Server", width=20, command=lambda: root.after(0, ask_for_server_details)).grid(row=3, column=0, pady=5)

# Connect to server button - now uses after() to call ask_for_client_details
tk.Button(root, text="Connect to Server", width=20, command=lambda: root.after(0, ask_for_client_details)).grid(row=3, column=1, pady=5)

root.mainloop()