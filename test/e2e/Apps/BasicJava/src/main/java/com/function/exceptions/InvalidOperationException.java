package com.function.exceptions;

public class InvalidOperationException extends Exception {
    public InvalidOperationException(String message) {
        super(message);
    }

    public InvalidOperationException(String message, Throwable cause) {
        super(message, cause);
    }

    public InvalidOperationException(Throwable cause) {
        super(cause);
    }

    public String toString() {
        return "System.InvalidOperationException: " + getMessage() + "\n" + getStackTrace();
    }
}
