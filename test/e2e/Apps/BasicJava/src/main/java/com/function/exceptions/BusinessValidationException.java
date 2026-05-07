package com.function.exceptions;

import java.time.Instant;
import java.util.List;
import java.util.Map;

public class BusinessValidationException extends Exception {
    private final String stringProperty;
    private final Integer intProperty;
    private final Long longProperty;
    private final Instant dateTimeProperty;
    private final Map<String, Object> dictionaryProperty;
    private final List<Object> listProperty;
    private final Object nullProperty;

    public BusinessValidationException(
            String message,
            String stringProperty,
            Integer intProperty,
            Long longProperty,
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

    public BusinessValidationException(String message) {
        super(message);
        this.stringProperty = null;
        this.intProperty = null;
        this.longProperty = null;
        this.dateTimeProperty = null;
        this.dictionaryProperty = null;
        this.listProperty = null;
        this.nullProperty = null;
    }

    public String getStringProperty() {
        return stringProperty;
    }

    public Integer getIntProperty() {
        return intProperty;
    }

    public Long getLongProperty() {
        return longProperty;
    }

    public Instant getDateTimeProperty() {
        return dateTimeProperty;
    }

    public Map<String, Object> getDictionaryProperty() {
        return dictionaryProperty;
    }

    public List<Object> getListProperty() {
        return listProperty;
    }

    public Object getNullProperty() {
        return nullProperty;
    }
}
