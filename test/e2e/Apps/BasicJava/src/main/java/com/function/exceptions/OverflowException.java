package com.function.exceptions;

public class OverflowException extends Exception {
    public OverflowException(String message) {
        super(message);
    }

    public OverflowException(String message, Throwable cause) {
        super(message, cause);
    }

    public OverflowException(Throwable cause) {
        super(cause);
    }

    public String toString() {
        return "System.OverflowException: " + getMessage() + "\n" + getStackTrace();
    }
}
