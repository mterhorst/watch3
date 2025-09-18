import { LitElement, html, css } from './lit-core.min.js';

class SimpleHeader extends LitElement {
    static styles = css`
        header {
            display: flex;
            align-items: center;
            padding: 0.75rem 1rem;
            border-bottom: 1px solid #fff;
            background: #3c3c3c;
        }

        .header-item {
            padding: 0.5rem 1rem;
            cursor: pointer;
            transition: background 0.3s, color 0.3s;
            text-decoration: none;
            color: inherit;
        }

        .header-item:hover {
            background: #e0e0e0;
            color: #007BFF;
        }
  `;

    render() {
        return html`
            <header>
                <a class="header-item" href="/">🏠 Home</a>
                <a class="header-item" href="/viewer.html">📄 viewer</a>
            </header>
    `;
    }
}

customElements.define('simple-header', SimpleHeader);