import azure.functions as func
import azure.durable_functions as df
import time

# an entity that counts requests, and sleeps for the number of seconds indicated
# by the input
def entity_function(context: df.DurableEntityContext):
    current_value = context.get_state(lambda: 0)
    current_value = current_value + 1
    context.set_state(current_value)
    input = context.get_input()
    time.sleep(input)
    context.set_result(current_value)

main = df.Entity.create(entity_function)
