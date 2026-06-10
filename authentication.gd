extends Control

const ADMIN_EMAIL = "admin123@gmail.com"
const ADMIN_PASSWORD = "admin654321"

var _adminBootstrapPending = false


# Called when the node enters the scene tree for the first time.
func _ready():
	Firebase.Auth.login_succeeded.connect(on_login_succeeded)
	Firebase.Auth.signup_succeeded.connect(on_signup_succeeded)
	Firebase.Auth.login_failed.connect(on_login_failed)
	Firebase.Auth.signup_failed.connect(on_signup_failed)
	
	if Firebase.Auth.check_auth_file():
		%StateLabel.text = "Logged in"
		get_tree().change_scene_to_file("res://Scenes/titlescreen.tscn")


func _process(delta):
	if Input.is_action_just_pressed("logout"):
		logout()


func _on_login_button_pressed():
	var email = %EmailLineEdit.text
	var password = %PasswordLineEdit.text
	_adminBootstrapPending = email == ADMIN_EMAIL and password == ADMIN_PASSWORD
	Firebase.Auth.login_with_email_and_password(email, password)
	%StateLabel.text = "Logging in"

func _on_sign_up_button_pressed():
	var email = %EmailLineEdit.text
	var password = %PasswordLineEdit.text
	Firebase.Auth.signup_with_email_and_password(email, password)
	%StateLabel.text = "Signing Up"


func on_login_succeeded(auth):
	_adminBootstrapPending = false
	%StateLabel.text = "Login success!"
	Firebase.Auth.save_auth(auth)
	if Firebase.Auth.check_auth_file():
		get_tree().change_scene_to_file("res://Scenes/titlescreen.tscn")
	
func on_signup_succeeded(auth):
	_adminBootstrapPending = false
	%StateLabel.text = "Sign up success!"
	Firebase.Auth.save_auth(auth)
	if Firebase.Auth.check_auth_file():
		get_tree().change_scene_to_file("res://Scenes/titlescreen.tscn")
	
func on_login_failed(error_code, message):
	print(error_code)
	print(message)
	if _adminBootstrapPending:
		_adminBootstrapPending = false
		%StateLabel.text = "Creating admin account"
		Firebase.Auth.signup_with_email_and_password(ADMIN_EMAIL, ADMIN_PASSWORD)
		return
	%StateLabel.text = "Login failed. Error: %s" % message
	
func on_signup_failed(error_code, message):
	print(error_code)
	print(message)
	_adminBootstrapPending = false
	%StateLabel.text = "Sign up failed. Error: %s" % message


func logout():
	Firebase.Auth.logout()
	%StateLabel.text = "Logged out"
	get_tree().change_scene_to_file("res://Scenes/Authentication.tscn")
