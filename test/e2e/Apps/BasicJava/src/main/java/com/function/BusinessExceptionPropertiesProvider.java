package com.function;

import java.util.LinkedHashMap;
import java.util.Map;

import com.function.exceptions.BusinessValidationException;
import com.microsoft.durabletask.ExceptionPropertiesProvider;

/**
 * Surfaces the custom properties carried by {@link BusinessValidationException} into
 * {@code FailureDetails.Properties}. Registered via SPI (see
 * {@code META-INF/services/com.microsoft.durabletask.ExceptionPropertiesProvider}), which is the
 * Java analogue of the .NET isolated DI registration and the JavaScript
 * {@code df.app.setExceptionPropertiesProvider(...)} call.
 */
public class BusinessExceptionPropertiesProvider implements ExceptionPropertiesProvider {

    @Override
    public Map<String, Object> getExceptionProperties(Exception exception) {
        if (exception instanceof BusinessValidationException) {
            BusinessValidationException ex = (BusinessValidationException) exception;
            Map<String, Object> properties = new LinkedHashMap<>();
            properties.put("StringProperty", ex.getStringProperty());
            properties.put("IntProperty", ex.getIntProperty());
            properties.put("LongProperty", ex.getLongProperty());
            properties.put("DateTimeProperty", ex.getDateTimeProperty());
            properties.put("DictionaryProperty", ex.getDictionaryProperty());
            properties.put("ListProperty", ex.getListProperty());
            properties.put("NullProperty", ex.getNullProperty());
            return properties;
        }
        return null;
    }
}
