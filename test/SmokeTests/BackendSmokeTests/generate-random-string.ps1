# Define the length of the random string
$length = 13

# Define the ASCII ranges for different character types
$uppercase_range = 65..90   # A-Z
$lowercase_range = 97..122  # a-z
$number_range = 48..57      # 0-9

# Define ASCII ranges for special characters:
# ! " # $ % & ' ( ) * + , - . / : ; < = > ? @ [ \ ] ^ _ ` { | } ~
$special_chars = 33..47 + 58..64 + 91..96 + 123..126  # Special characters

# Combine all the ranges
$all_chars = $uppercase_range + $lowercase_range + $number_range + $special_chars

# Generate a random string
$newString = -join ($all_chars | Get-Random -Count $length | ForEach-Object {[char]$_})

# Output the new string
Write-Output $newString