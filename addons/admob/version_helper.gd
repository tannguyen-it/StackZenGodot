# MIT License

# Copyright (c) 2023-present Poing Studios

# Permission is hereby granted, free of charge, to any person obtaining a copy
# of this software and associated documentation files (the "Software"), to deal
# in the Software without restriction, including without limitation the rights
# to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
# copies of the Software, and to permit persons to whom the Software is
# furnished to do so, subject to the following conditions:

# The above copyright notice and this permission notice shall be included in all
# copies or substantial portions of the Software.

# THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
# IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
# FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
# AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
# LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
# OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
# SOFTWARE.

class_name PoingAdMobVersionHelper
extends Object

static func get_plugin_version() -> String:
	var version: String = "v3.1.2" # fallback (redundancy)

	# Godot 4: kiểm tra file tồn tại trước khi load để không spam error
	if not FileAccess.file_exists("res://addons/admob/plugin.cfg"):
		return version

	var plugin_config_file := ConfigFile.new()
	var err := plugin_config_file.load("res://addons/admob/plugin.cfg")
	if err == OK:
		# get_value có thể null nếu thiếu key
		var v = plugin_config_file.get_value("plugin", "version", version)
		if typeof(v) == TYPE_STRING and v != "":
			version = v
	return version

static func _get_plugin_version_formated() -> String:
	var version := get_plugin_version()
	
	var pattern = RegEx.new()
	pattern.compile("(?:v)?(\\d+\\.\\d+\\.\\d+)")
	
	var matchs := pattern.search(version)
	if matchs != null:
		version = matchs.get_string(1)
	return version

