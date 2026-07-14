import React from "react";
import { createRoot } from "react-dom/client";
import { AdminApp } from "./AdminApp";
import { App } from "./App";
import "./styles.css";
import "./drawing-patterns.css";
import "./release.css";
import "./admin.css";

const isAdminPath = window.location.pathname.replace(/\/$/, "").endsWith("/admin")
  || new URLSearchParams(window.location.search).has("admin");

createRoot(document.getElementById("root")).render(isAdminPath ? <AdminApp /> : <App />);
