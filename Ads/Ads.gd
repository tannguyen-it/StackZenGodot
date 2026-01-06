extends Node
class_name Ads

# ====== CONFIG ======
@export var interstitial_every_n_gameovers: int = 5

# Google TEST IDs (safe for local testing)
const ANDROID_BANNER_ID := "ca-app-pub-4144794869067184/3697213524"
const ANDROID_INTERSTITIAL_ID := "ca-app-pub-4144794869067184/2384131854"

# (Optional) iOS test IDs
const IOS_BANNER_ID := "ca-app-pub-3940256099942544/2934735716"
const IOS_INTERSTITIAL_ID := "ca-app-pub-3940256099942544/4411468910"

var banner = null
var interstitial_ad = null
var interstitial_cb := InterstitialAdLoadCallback.new()

var _gameover_count: int = 0
var _initialized: bool = false

func _ready():
	# Only run on mobile exports
	var os_name := OS.get_name()
	if os_name != "Android" and os_name != "iOS":
		return

	# Init SDK once
	print("[Ads] READY platform=", OS.get_name())
	MobileAds.initialize()
	print("[Ads] initialize called")
	_initialized = true

	# Setup callbacks for interstitial
	interstitial_cb.on_ad_loaded = func(ad):
		interstitial_ad = ad
		print("[Ads] Interstitial loaded")

	interstitial_cb.on_ad_failed_to_load = func(err):
		interstitial_ad = null
		print("[Ads] Interstitial failed: ", err.message)

	# Preload interstitial for later
	load_interstitial()

func _banner_unit_id() -> String:
	return ANDROID_BANNER_ID if OS.get_name() == "Android" else IOS_BANNER_ID

func _interstitial_unit_id() -> String:
	return ANDROID_INTERSTITIAL_ID if OS.get_name() == "Android" else IOS_INTERSTITIAL_ID

# ====== Banner ======
func show_banner_bottom():
	if not _initialized:
		return
	if banner != null:
		return
	banner = AdView.new(_banner_unit_id(), AdSize.BANNER, AdPosition.Values.BOTTOM)
	banner.load_ad(AdRequest.new())
	print("[Ads] Banner requested")

func hide_banner():
	if banner != null:
		banner.destroy()
		banner = null
		print("[Ads] Banner hidden")

# ====== Interstitial ======
func load_interstitial():
	if not _initialized:
		return
	if interstitial_ad != null:
		return
	InterstitialAdLoader.new().load(_interstitial_unit_id(), AdRequest.new(), interstitial_cb)
	print("[Ads] Interstitial load requested")

func show_interstitial():
	if not _initialized:
		return
	if interstitial_ad != null:
		interstitial_ad.show()
		interstitial_ad = null
		print("[Ads] Interstitial show")
	else:
		print("[Ads] Interstitial not ready")

	# Always load again for next time
	load_interstitial()

# ====== Hooks called from C# ======
func on_start_screen():
	show_banner_bottom()

func on_gameplay_start():
	hide_banner()

func on_game_over():
	show_banner_bottom()

	_gameover_count += 1
	if interstitial_every_n_gameovers > 0 and (_gameover_count % interstitial_every_n_gameovers == 0):
		show_interstitial()
	else:
		load_interstitial()
