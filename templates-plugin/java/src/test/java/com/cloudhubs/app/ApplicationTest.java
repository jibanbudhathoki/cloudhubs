package com.cloudhubs.app;

import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.assertNotNull;

public class ApplicationTest {
    @Test
    void contextLoads() {
        Application app = new Application();
        assertNotNull(app);
    }
}

