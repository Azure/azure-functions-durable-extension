package com.function.exceptions;

import java.time.Instant;
import java.util.List;
import java.util.Map;

/**
 * Exception that carries custom, structured properties. Mirrors the BusinessValidationException used
 * by the BasicNode and BasicDotNetIsolated e2e apps so the custom-exception-properties feature can
 * be validated for Java exactly as it is for the other languages.
 */
public class BusinessValidationException extends Exception {

    private final String stringProperty;
    private final int intProperty;
    private final long longProperty;
    private final Instant dateTimeProperty;
    private final Map<String, Object> dictionaryProperty;
    private final List<Object> listProperty;
    private final Object nullProperty;

    public BusinessValidationException(
            String message,
            String stringProperty,
            int intProperty,
            long longProperty,
            Instant dateTimeProperty,
            Map<String, Object> dictionaryProperty,
            List<Object> listProperty,
            Object nullProperty) {
        super(message);
        this.stringProperty = stringProperty;
        this.intProperty = intProperty;
        this.longProperty = longProperty;
        this.dateTimeProperty = dateTimeProperty;
        this.dictionaryProperty = dictionaryProperty;
        this.listProperty = listProperty;
        this.nullProperty = nullProperty;
    }

    public String getStringProperty() {
        return this.stringProperty;
    }

    public int getIntProperty() {
        return this.intProperty;
    }

    public long getLongProperty() {
        return this.longProperty;
    }

    public Instant getDateTimeProperty() {
        return this.dateTimeProperty;
    }

    public Map<String, Object> getDictionaryProperty() {
        return this.dictionaryProperty;
    }

    public List<Object> getListProperty() {
        return this.listProperty;
    }

    public Object getNullProperty() {
        return this.nullProperty;
    }
}
