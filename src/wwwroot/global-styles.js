import { LitElement, html, css } from './lit-core.min.js';
import './register_subscription.js'

const globalStyles = css`
  body {
    margin: 0;
    font: 500 16px system-ui, sans-serif;
  }
`;

class GlobalStyles extends LitElement {
    render() {
        return html`
            <register-subscription></register-subscription>
        `;
    }

    connectedCallback() {
        super.connectedCallback();
        const style = document.createElement('style');
        style.textContent = globalStyles.cssText;
        document.head.appendChild(style);

        this.addMetaTag('name', 'viewport', 'width=device-width, initial-scale=1');
        this.addMetaTag('name', 'color-scheme', 'only dark');
    }

    addMetaTag(attr, value, content = null) {
        const meta = document.createElement('meta');
        meta.setAttribute(attr, value);
        if (content) {
            meta.setAttribute('content', content);
        }
        document.head.appendChild(meta);
    }
}

customElements.define('global-styles', GlobalStyles);