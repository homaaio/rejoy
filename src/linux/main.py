# src/linux/main.py
#!/usr/bin/env python3
import threading
import time
import json
import os
from joystick_reader import JoystickReader
from keyboard_emulator import KeyboardEmulator
from joystick_hider import JoystickHider
from web_server import start_web_server
from gui import DualKeyGUI

DEFAULT_BINDINGS = {
    'left_stick_up': 'w',
    'left_stick_down': 's',
    'left_stick_left': 'a',
    'left_stick_right': 'd',
    'right_stick_up': 'up',
    'right_stick_down': 'down',
    'right_stick_left': 'left',
    'right_stick_right': 'right',
    'dpad_up': 'up',
    'dpad_down': 'down',
    'dpad_left': 'left',
    'dpad_right': 'right',
    'cross': 'space',
    'circle': 'e',
    'triangle': 'q',
    'square': 'r',
    'l1': 'shift',
    'r1': 'ctrl',
    'l2': '1',
    'r2': '2',
    'l3': 'f',
    'r3': 'g',
    'select': 'tab',
    'start': 'enter',
    'ps_button': 'esc',
}

class DualKeyLinux:
    def __init__(self):
        self.bindings = DEFAULT_BINDINGS.copy()
        self.player_bindings = {i: DEFAULT_BINDINGS.copy() for i in range(1, 5)}
        self.current_player = 1
        self.emulation_enabled = False
        self.deadzone = 0.3
        self.running = True
        self.lock = threading.Lock()
        
        self.reader = JoystickReader()
        self.emulator = KeyboardEmulator()
        self.hider = JoystickHider()
        
        # Настройки индикаторов
        self.indicators_enabled = True
        self.indicator_mode = 0  # 0=Static, 1=Blink, 2=Running, 3=Alternating
        self.indicator_speed = 500
        self.indicator_step = 0
        self.indicator_blink_state = False
        self.indicator_colors = ['#ff0000', '#ff0000', '#ff0000', '#ff0000']
        
        self.config_file = os.path.expanduser('~/.dualkey_config.json')
        self.load_config()
    
    def get_state(self):
        with self.lock:
            return {
                'connected': self.reader.connected,
                'emulation': self.emulation_enabled,
                'hidden': self.hider.is_hidden,
                'deadzone': self.deadzone,
                'current_player': self.current_player,
                'bindings': dict(self.bindings),
                'axes': dict(self.reader.axes),
                'buttons': dict(self.reader.buttons),
                'dpad': dict(self.reader.dpad),
                'indicators': {
                    'enabled': self.indicators_enabled,
                    'mode': self.indicator_mode,
                    'speed': self.indicator_speed,
                    'colors': self.indicator_colors,
                }
            }
    
    def switch_player(self, player):
        if 1 <= player <= 4:
            self.current_player = player
            if player in self.player_bindings:
                self.bindings = self.player_bindings[player].copy()
            self.save_config()
    
    def process_emulation(self):
        if not self.emulation_enabled:
            return
        
        dz = self.deadzone
        
        lx = self.reader.axes.get('left_x', 0)
        ly = self.reader.axes.get('left_y', 0)
        rx = self.reader.axes.get('right_x', 0)
        ry = self.reader.axes.get('right_y', 0)
        
        if lx < -dz:
            self.emulator.press('left_stick_left', self.bindings)
        elif lx > dz:
            self.emulator.press('left_stick_right', self.bindings)
        else:
            self.emulator.release('left_stick_left', self.bindings)
            self.emulator.release('left_stick_right', self.bindings)
        
        if ly < -dz:
            self.emulator.press('left_stick_up', self.bindings)
        elif ly > dz:
            self.emulator.press('left_stick_down', self.bindings)
        else:
            self.emulator.release('left_stick_up', self.bindings)
            self.emulator.release('left_stick_down', self.bindings)
        
        if rx < -dz:
            self.emulator.press('right_stick_left', self.bindings)
        elif rx > dz:
            self.emulator.press('right_stick_right', self.bindings)
        else:
            self.emulator.release('right_stick_left', self.bindings)
            self.emulator.release('right_stick_right', self.bindings)
        
        if ry < -dz:
            self.emulator.press('right_stick_up', self.bindings)
        elif ry > dz:
            self.emulator.press('right_stick_down', self.bindings)
        else:
            self.emulator.release('right_stick_up', self.bindings)
            self.emulator.release('right_stick_down', self.bindings)
        
        button_map = {
            'cross': 'cross', 'circle': 'circle', 'triangle': 'triangle',
            'square': 'square', 'l1': 'l1', 'r1': 'r1',
            'l3': 'l3', 'r3': 'r3',
            'select': 'select', 'start': 'start', 'ps': 'ps_button',
        }
        
        for btn_name, action in button_map.items():
            if self.reader.buttons.get(btn_name, False):
                self.emulator.press(action, self.bindings)
            else:
                self.emulator.release(action, self.bindings)
        
        dpad_map = {
            'up': 'dpad_up', 'down': 'dpad_down',
            'left': 'dpad_left', 'right': 'dpad_right',
        }
        
        for dpad_dir, action in dpad_map.items():
            if self.reader.dpad.get(dpad_dir, False):
                self.emulator.press(action, self.bindings)
            else:
                self.emulator.release(action, self.bindings)
    
    def save_config(self):
        config = {
            'deadzone': self.deadzone,
            'current_player': self.current_player,
            'player_bindings': self.player_bindings,
            'indicators_enabled': self.indicators_enabled,
            'indicator_mode': self.indicator_mode,
            'indicator_speed': self.indicator_speed,
            'indicator_colors': self.indicator_colors,
        }
        try:
            with open(self.config_file, 'w') as f:
                json.dump(config, f, indent=2)
        except Exception as e:
            print(f"Error saving config: {e}")
    
    def load_config(self):
        if not os.path.exists(self.config_file):
            return
        
        try:
            with open(self.config_file, 'r') as f:
                config = json.load(f)
            
            if 'deadzone' in config:
                self.deadzone = float(config['deadzone'])
            if 'current_player' in config:
                self.current_player = int(config['current_player'])
            if 'player_bindings' in config:
                self.player_bindings = {int(k): v for k, v in config['player_bindings'].items()}
                if self.current_player in self.player_bindings:
                    self.bindings = self.player_bindings[self.current_player].copy()
            if 'indicators_enabled' in config:
                self.indicators_enabled = config['indicators_enabled']
            if 'indicator_mode' in config:
                self.indicator_mode = config['indicator_mode']
            if 'indicator_speed' in config:
                self.indicator_speed = config['indicator_speed']
            if 'indicator_colors' in config:
                self.indicator_colors = config['indicator_colors']
        except Exception as e:
            print(f"Error loading config: {e}")
    
    def run(self):
        print("DualKey Linux starting...")
        print("GUI opened in separate window")
        print("Web interface: http://localhost:8080")
        
        web_thread = threading.Thread(target=start_web_server, args=(self,), daemon=True)
        web_thread.start()
        
        gui = DualKeyGUI(self)
        
        def update_loop():
            while self.running:
                try:
                    self.reader.update()
                    self.process_emulation()
                except Exception as e:
                    print(f"Update error: {e}")
                time.sleep(0.016)
        
        update_thread = threading.Thread(target=update_loop, daemon=True)
        update_thread.start()
        
        try:
            gui.run()
        except KeyboardInterrupt:
            print("\nShutting down...")
        finally:
            self.emulator.release_all(self.bindings)
            self.running = False
            self.save_config()

if __name__ == '__main__':
    app = DualKeyLinux()
    app.run()
