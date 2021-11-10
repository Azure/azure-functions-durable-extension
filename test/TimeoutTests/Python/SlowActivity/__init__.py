import azure.functions as func
import azure.durable_functions as df
import time

# an activity that sleeps for the number of seconds indicated by the input
def main(input: str) -> str:
    seconds = float(input)
    time.sleep(seconds)
    return f"Slept for {seconds} seconds."
