from functools import partial
import socket
import threading
import tkinter as tk
from tkinter import scrolledtext, simpledialog
from datetime import datetime

# Global socket and variables
chat_socket = None
connected = False
game_started = False
turn = 0  # 0 for Player 1 (X), 1 for Player 2 (O)
players = []
game_board = [[" " for _ in range(3)] for _ in range(3)]

# Create GUI
root = tk.Tk()
root.title("Live TCP Chat with Tic-Tac-Toe")

chat_display = scrolledtext.ScrolledText(root, width=60, height=15, state='disabled')
chat_display.grid(row=0, column=0, columnspan=2, padx=10, pady=10)

typing_label = tk.Label(root, text="", fg="grey")
typing_label.grid(row=1, column=0, columnspan=2)

# Game Status and Board Display
game_status_label = tk.Label(root, text="Waiting for Players...", fg="blue")
game_status_label.grid(row=2, column=0, columnspan=2)

game_buttons = []

# for i in range(3):
#     row_buttons = []
#     for j in range(3):
#         button = tk.Button(root, text=" ", width=10, height=3, command=lambda i=i, j=j: make_move(i, j))
#         button.grid(row=3 + i, column=j, padx=5, pady=5)
#         row_buttons.append(button)
#     game_buttons.append(row_buttons)
for i in range(3):
    row_buttons = []
    for j in range(3):
        # Pass i and j as default arguments to the lambda function to capture their current values
        button = tk.Button(root, text=" ", width=10, height=3, command=partial(make_move, i, j))
        button.grid(row=3 + i, column=j, padx=5, pady=5)
        row_buttons.append(button)
    game_buttons.append(row_buttons)

def log(msg):
    chat_display['state'] = 'normal'
    chat_display.insert(tk.END, msg + '\n')
    chat_display['state'] = 'disabled'
    chat_display.see(tk.END)

def get_timestamp():
    return datetime.now().strftime("%Y-%m-%d %H:%M:%S")

def receive_messages():
    global chat_socket, connected, players
    while connected:
        try:
            message = chat_socket.recv(1024).decode()
            if message.startswith("<GAME_MOVE>"):
                _, row, col, player = message.split(",")
                row, col = int(row), int(col)
                handle_game_move(row, col, player)
            elif message == "<START_GAME>":
                start_game()
            elif message == "<END_GAME>":
                end_game()
            elif message == "<TYPING>":
                typing_label.config(text="User is typing...")
            elif message == "<STOP_TYPING>":
                typing_label.config(text="")
            else:
                log(f"[{get_timestamp()}] Peer: {message}")
        except:
            break

def start_game():
    global game_started, game_board, turn
    game_board = [[" " for _ in range(3)] for _ in range(3)]
    turn = 0
    game_started = True
    update_game_status("Game Started! Player 1's turn.")
    update_game_buttons()

def end_game():
    global game_started
    game_started = False
    update_game_status("Game Over!")
    for row_buttons in game_buttons:
        for button in row_buttons:
            button.config(state='disabled')

def update_game_status(message):
    game_status_label.config(text=message)

def update_game_buttons():
    for i in range(3):
        for j in range(3):
            game_buttons[i][j].config(text=game_board[i][j])

def handle_game_move(row, col, player):
    global game_board, turn
    if game_board[row][col] == " " and game_started:
        game_board[row][col] = 'X' if player == "1" else 'O'
        turn = 1 - turn
        update_game_buttons()
        check_win_condition()

def make_move(row, col):
    global turn, chat_socket
    if game_started and game_board[row][col] == " ":
        if (turn == 0 and players[0] == "1") or (turn == 1 and players[1] == "2"):
            game_board[row][col] = 'X' if turn == 0 else 'O'
            update_game_buttons()
            chat_socket.send(f"<GAME_MOVE>{row},{col},{turn+1}".encode())
            check_win_condition()
            turn = 1 - turn
            update_game_status(f"Player {turn+1}'s turn.")

def check_win_condition():
    for i in range(3):
        # Check rows and columns
        if game_board[i][0] == game_board[i][1] == game_board[i][2] != " ":
            update_game_status(f"Player {turn+1} wins!")
            chat_socket.send("<END_GAME>".encode())
            return
        if game_board[0][i] == game_board[1][i] == game_board[2][i] != " ":
            update_game_status(f"Player {turn+1} wins!")
            chat_socket.send("<END_GAME>".encode())
            return

    # Check diagonals
    if game_board[0][0] == game_board[1][1] == game_board[2][2] != " ":
        update_game_status(f"Player {turn+1} wins!")
        chat_socket.send("<END_GAME>".encode())
        return
    if game_board[0][2] == game_board[1][1] == game_board[2][0] != " ":
        update_game_status(f"Player {turn+1} wins!")
        chat_socket.send("<END_GAME>".encode())
        return

    # Check if the game is a draw
    if all(game_board[i][j] != " " for i in range(3) for j in range(3)):
        update_game_status("It's a draw!")
        chat_socket.send("<END_GAME>".encode())

def ask_for_server_details():
    host = simpledialog.askstring("Server", "Enter IP to bind (e.g. 0.0.0.0):", initialvalue="0.0.0.0")
    port = int(simpledialog.askstring("Server", "Enter port:", initialvalue="5555"))
    start_server(host, port)

def start_server(host, port):
    global chat_socket, connected, players
    server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server.bind((host, port))
    server.listen(2)  # Listen for 2 clients (Server + 2 players)
    log(f"[SERVER] Listening on {host}:{port}...")
    chat_socket, addr = server.accept()
    connected = True
    players.append("1")  # Player 1 (Server)
    log(f"[SERVER] Connected to {addr}")
    threading.Thread(target=receive_messages, daemon=True).start()
    chat_socket.send("<START_GAME>".encode())  # Start the game after connection

def ask_for_client_details():
    host = simpledialog.askstring("Client", "Enter server IP:", initialvalue="127.0.0.1")
    port = int(simpledialog.askstring("Client", "Enter server port:", initialvalue="5555"))
    connect_to_server(host, port)

def connect_to_server(host, port):
    global chat_socket, connected, players
    chat_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    chat_socket.connect((host, port))
    connected = True
    players.append("2")  # Player 2 (Client)
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
        send_typing_signal()
        is_typing = True
    if typing_timer:
        root.after_cancel(typing_timer)
    typing_timer = root.after(1000, stop_typing_signal)

# Buttons and Entry
message_entry = tk.Entry(root, width=45)
message_entry.grid(row=2, column=0, padx=10, pady=5)

message_entry.bind("<KeyPress>", on_typing)

tk.Button(root, text="Send", width=15, command=send_message).grid(row=2, column=1, pady=5)
tk.Button(root, text="Start as Server", width=20, command=lambda: root.after(0, ask_for_server_details)).grid(row=3, column=0, pady=5)
tk.Button(root, text="Connect to Server", width=20, command=lambda: root.after(0, ask_for_client_details)).grid(row=3, column=1, pady=5)

root.mainloop()