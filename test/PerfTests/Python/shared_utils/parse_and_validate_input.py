def parse_and_validate_input(input_: any) -> int:
    try:
        input_ = int(input_)        
    except:
        raise Exception(f"Input was expected to be compatible with type int, but received: {type(input_)}")
    if input_ < 1:
        raise Exception(f"Input was expect to be larger or equal to 1, but received: {input_}")
    return input_