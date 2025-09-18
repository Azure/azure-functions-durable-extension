package com.function.JsonHelpers;

import java.time.Instant;

import com.google.gson.Gson;
import com.google.gson.GsonBuilder;

public class DurableMetadataGsonProvider {
    public static Gson createGson() {
        return new GsonBuilder()
            .registerTypeAdapter(Instant.class, new InstantAdapter())
            .create();
    }
}
